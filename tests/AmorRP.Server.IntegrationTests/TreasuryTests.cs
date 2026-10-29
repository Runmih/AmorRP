using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private static string TreasuryPath(Community c, GroupProfile g) => $"/api/v2/communities/{c.Id}/groups/{g.Id}/treasury";
    private async Task<GroupProfile> TreasuryGroup(HttpClient owner, Community c, string name = "Treasury")
    {
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite(name, ""));
        Assert.DoesNotContain(GroupCapabilities.Treasury, group.EnabledFeatures!);
        return await Command<GroupProfile>(owner, HttpMethod.Put, $"/api/v2/communities/{c.Id}/groups/{group.Id}/features", new GroupFeaturesEdit(GroupCapabilities.Features.ToArray()), group.Etag);
    }
    private Task<Balance> Fund(HttpClient owner, Community c, Guid character, string amount) =>
        Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/currency/adjustments", new CurrencyAdjustment(character, amount, "Treasury test funds"));
    private Task<TreasuryTransferResult> TreasuryMove(HttpClient actor, Community c, GroupProfile g, string amount, bool deposit = true) =>
        Command<TreasuryTransferResult>(actor, HttpMethod.Post, TreasuryPath(c, g) + (deposit ? "/deposits" : "/withdrawals"), new TreasuryTransfer(amount, "Test transfer"));

    [Fact]
    public async Task Treasury_approval_delegation_records_and_revocation_preserve_group_authority()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var admin = f.CreateClient(); await Login(admin, "1200"); var c = await Create(admin);
        using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1201")).Character.Id; var invite = await Invite(admin, c); await Join(owner, invite);
        await GrantHome(admin, c, ownerId, "groups.create");
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Staff treasury", "")); var path = TreasuryPath(c, group); var groupPath = path[..^9];
        using (var noApproval = await owner.GetAsync(path, ct)) Assert.Equal(HttpStatusCode.Forbidden, noApproval.StatusCode);
        group = await Command<GroupProfile>(admin, HttpMethod.Put, groupPath + "/features", new GroupFeaturesEdit(GroupCapabilities.Features.ToArray()), group.Etag);
        using var staff = f.CreateClient(); var staffId = (await Login(staff, "1202")).Character.Id; await Join(staff, invite); await GroupAction(staff, HttpMethod.Post, groupPath + "/join", new GroupJoin());
        await Fund(admin, c, staffId, "100");
        var deposited = await TreasuryMove(staff, c, group, "60"); Assert.Equal("40", deposited.CharacterBalance.Owned); Assert.Equal("60", deposited.Treasury.Owned);
        using (var denied = await staff.GetAsync(path + "/records", ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var req = Request(HttpMethod.Post, path + "/withdrawals", new TreasuryTransfer("1")))
        using (var denied = await staff.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        foreach (var suffix in new[] { "", "/records" })
            using (var denied = await admin.GetAsync(path + suffix, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantStaff(owner, groupPath, staffId, GroupCapabilities.TreasuryRecords);
        var records = (await staff.GetFromJsonAsync<TreasuryReceiptPage>(path + "/records", ct))!; Assert.Single(records.Items); Assert.Equal("Test transfer", records.Items[0].Note);
        await GrantStaff(owner, groupPath, staffId, GroupCapabilities.TreasuryWithdraw);
        var withdrawal = await TreasuryMove(staff, c, group, "20", false); Assert.Equal("40", withdrawal.Treasury.Owned); Assert.Equal("60", withdrawal.CharacterBalance.Owned);
        using (var denied = await staff.GetAsync(path + "/records", ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        group = await Command<GroupProfile>(admin, HttpMethod.Put, groupPath + "/features", new GroupFeaturesEdit(GroupCapabilities.DefaultFeatures.ToArray()), group.Etag);
        Assert.Empty((await StaffRow(owner, groupPath, staffId)).Capabilities!);
        using (var req = Request(HttpMethod.Post, path + "/withdrawals", new TreasuryTransfer("1")))
        using (var denied = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        group = await Command<GroupProfile>(admin, HttpMethod.Put, groupPath + "/features", new GroupFeaturesEdit(GroupCapabilities.Features.ToArray()), group.Etag);
        Assert.Empty((await staff.GetFromJsonAsync<GroupProfile>(groupPath, ct))!.MyCapabilities!);
        Assert.Equal("40", (await owner.GetFromJsonAsync<TreasuryBalance>(path, ct))!.Owned);
    }

    [Fact]
    public async Task Treasury_deposits_respect_trade_reservations_and_durable_replay_does_not_repeat_money()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1210")).Character.Id; var c = await Create(owner); var group = await TreasuryGroup(owner, c); var path = TreasuryPath(c, group);
        using var peer = f.CreateClient(); var peerId = (await Login(peer, "1211")).Character.Id; await Join(peer, await Invite(owner, c));
        await Fund(owner, c, ownerId, "100");
        var trade = await Command<Trade>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/trades", new TradeCreate(peerId)); trade = await TradeCommand(peer, c, trade, "accept"); trade = await Offer(owner, c, trade, "70");
        using (var req = Request(HttpMethod.Post, path + "/deposits", new TreasuryTransfer("31")))
        using (var denied = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        var body = new TreasuryTransfer("30", "Only available funds"); var key = Guid.CreateVersion7(clock.GetUtcNow()); string? committed = null;
        for (var i = 0; i < 2; i++)
        {
            using var req = Request(HttpMethod.Post, path + "/deposits", body, key); using var response = await owner.SendAsync(req, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); var json = await response.Content.ReadAsStringAsync(ct);
            if (i == 0) committed = json; else Assert.Equal(committed, json);
        }
        using (var mismatch = Request(HttpMethod.Post, path + "/deposits", body with { Amount = "1" }, key))
        using (var denied = await owner.SendAsync(mismatch, ct)) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        var balance = (await owner.GetFromJsonAsync<BalancePage>($"/api/v2/communities/{c.Id}/balances", ct))!.Items.Single(); Assert.Equal("70", balance.Owned); Assert.Equal("70", balance.Reserved);
        await TradeCommand(owner, c, trade, "cancel");
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Single(await db.TreasuryEntries.ToArrayAsync(ct)); Assert.Equal(100, await db.Memberships.SumAsync(x => x.Owned, ct) + await db.Groups.SumAsync(x => x.TreasuryOwned, ct));
    }

    [Fact]
    public async Task Simultaneous_treasury_withdrawals_conserve_currency_and_never_overdraw()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1220")).Character.Id; var c = await Create(owner); var group = await TreasuryGroup(owner, c);
        await Fund(owner, c, ownerId, "100"); await TreasuryMove(owner, c, group, "100");
        var tasks = Enumerable.Range(0, 2).Select(async _ => { using var req = Request(HttpMethod.Post, TreasuryPath(c, group) + "/withdrawals", new TreasuryTransfer("75")); return await owner.SendAsync(req, ct); });
        var replies = await Task.WhenAll(tasks); Assert.Single(replies, x => x.IsSuccessStatusCode); Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var reply in replies) reply.Dispose();
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(25, (await db.Groups.SingleAsync(ct)).TreasuryOwned); Assert.Equal(75, (await db.Memberships.SingleAsync(ct)).Owned); Assert.Equal(2, await db.TreasuryEntries.CountAsync(ct));
    }

    [Fact]
    public async Task Withdraw_racing_disband_pays_staff_or_actual_owner_once_and_conserves_money()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1230")).Character.Id; var c = await Create(owner); var group = await TreasuryGroup(owner, c); var path = TreasuryPath(c, group);
        using var staff = f.CreateClient(); var staffId = (await Login(staff, "1231")).Character.Id; await Join(staff, await Invite(owner, c)); await GroupAction(staff, HttpMethod.Post, path[..^9] + "/join", new GroupJoin()); await GrantStaff(owner, path[..^9], staffId, GroupCapabilities.TreasuryWithdraw);
        await Fund(owner, c, ownerId, "100"); await TreasuryMove(owner, c, group, "100");
        using var remove = Request(HttpMethod.Delete, path[..^9], etag: group.Etag); using var withdraw = Request(HttpMethod.Post, path + "/withdrawals", new TreasuryTransfer("25"));
        var replies = await Task.WhenAll(owner.SendAsync(remove, ct), staff.SendAsync(withdraw, ct)); Assert.True(replies[0].IsSuccessStatusCode); Assert.Contains(replies[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound }); foreach (var reply in replies) reply.Dispose();
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(0, (await db.Groups.SingleAsync(ct)).TreasuryOwned); var members = await db.Memberships.ToArrayAsync(ct); Assert.Equal(100, members.Sum(x => x.Owned));
        var returned = await db.TreasuryEntries.SingleAsync(x => x.Kind == "disband_return", ct); Assert.Equal(ownerId, returned.CharacterId); Assert.Contains(returned.Amount, new long[] { 75, 100 });
    }

    [Fact]
    public async Task Moderator_disband_pays_the_inactive_group_owner_and_replays_after_server_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var admin = f.CreateClient(); var adminToken = await Login(admin, "1240"); var c = await Create(admin);
        using var owner = f.CreateClient(); var ownerToken = await Login(owner, "1241"); await Join(owner, await Invite(admin, c)); await GrantHome(admin, c, ownerToken.Character.Id, "groups.create");
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Departed", "")); var path = TreasuryPath(c, group);
        group = await Command<GroupProfile>(admin, HttpMethod.Put, path[..^9] + "/features", new GroupFeaturesEdit(GroupCapabilities.Features.ToArray()), group.Etag);
        await Fund(admin, c, ownerToken.Character.Id, "90"); await TreasuryMove(owner, c, group, "60");
        var member = await Member(admin, c, ownerToken.Character.Id); await Command<Member>(admin, HttpMethod.Delete, $"/api/v2/communities/{c.Id}/members/{ownerToken.Character.Id}", new MemberRemoval("Left community"), member.Etag);
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        using (var request = Request(HttpMethod.Delete, path[..^9], key: key, etag: group.Etag))
        using (var reply = await admin.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        using var restarted = Factory(); using var client = restarted.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken.AccessToken);
        using (var request = Request(HttpMethod.Delete, path[..^9], key: key, etag: group.Etag))
        using (var reply = await client.SendAsync(request, ct)) { Assert.Equal(HttpStatusCode.OK, reply.StatusCode); Assert.True(reply.Headers.Contains("Idempotency-Replayed")); }
        var records = (await owner.GetFromJsonAsync<TreasuryReceiptPage>(path + "/records", ct))!; Assert.Equal(2, records.Items.Length);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var accounts = await db.Memberships.ToArrayAsync(ct); Assert.Equal(90, accounts.Single(x => x.CharacterId == ownerToken.Character.Id).Owned); Assert.Equal(0, accounts.Single(x => x.CharacterId == adminToken.Character.Id).Owned);
        Assert.Single(await db.TreasuryEntries.Where(x => x.Kind == "disband_return").ToArrayAsync(ct));
    }

    [Fact]
    public async Task Community_deletion_settles_all_groups_to_their_distinct_owners()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var admin = f.CreateClient(); var adminId = (await Login(admin, "1250")).Character.Id; var c = await Create(admin); var a = await TreasuryGroup(admin, c, "A");
        using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1251")).Character.Id; await Join(owner, await Invite(admin, c)); await GrantHome(admin, c, ownerId, "groups.create");
        var b = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("B", "")); b = await Command<GroupProfile>(admin, HttpMethod.Put, TreasuryPath(c, b)[..^9] + "/features", new GroupFeaturesEdit(GroupCapabilities.Features.ToArray()), b.Etag);
        await Fund(admin, c, adminId, "100"); await Fund(admin, c, ownerId, "200"); await TreasuryMove(admin, c, a, "100"); await TreasuryMove(owner, c, b, "200");
        c = (await admin.GetFromJsonAsync<Community>($"/api/v2/communities/{c.Id}", ct))!;
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        for (var i = 0; i < 2; i++)
        {
            using var req = Request(HttpMethod.Post, $"/api/v2/communities/{c.Id}/deletion", new CommunityDeletion(c.Name), key, $"\"community:{c.Id:N}:{c.Version}\""); using var reply = await admin.SendAsync(req, ct);
            // The existing deletion endpoint intentionally becomes inaccessible after deletion.
            Assert.Equal(i == 0 ? HttpStatusCode.OK : HttpStatusCode.NotFound, reply.StatusCode);
        }
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.All(await db.Groups.ToArrayAsync(ct), x => { Assert.True(x.Deleted); Assert.Equal(0, x.TreasuryOwned); });
        var accounts = await db.Memberships.ToArrayAsync(ct); Assert.Equal(100, accounts.Single(x => x.CharacterId == adminId).Owned); Assert.Equal(200, accounts.Single(x => x.CharacterId == ownerId).Owned);
        Assert.Equal(2, await db.TreasuryEntries.CountAsync(x => x.Kind == "disband_return", ct));
    }

    [Fact]
    public async Task Balance_overflow_preserves_pending_return_and_collection_is_owner_only_and_replay_safe()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var id = (await Login(owner, "1260")).Character.Id; var c = await Create(owner); var group = await TreasuryGroup(owner, c); var path = TreasuryPath(c, group);
        await Fund(owner, c, id, "100"); await TreasuryMove(owner, c, group, "100"); await Fund(owner, c, id, long.MaxValue.ToString());
        await Command<GroupProfile>(owner, HttpMethod.Delete, path[..^9], etag: group.Etag);
        var returns = (await owner.GetFromJsonAsync<TreasuryReturnPage>("/api/v2/me/treasury-returns", ct))!; Assert.Equal("100", Assert.Single(returns.Items).Amount);
        using var other = f.CreateClient(); await Login(other, "1261");
        Assert.Empty((await other.GetFromJsonAsync<TreasuryReturnPage>("/api/v2/me/treasury-returns", ct))!.Items);
        using (var req = Request(HttpMethod.Post, path + "/return")) using (var denied = await other.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Fund(owner, c, id, "-60");
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        for (var i = 0; i < 2; i++)
        { using var req = Request(HttpMethod.Post, path + "/return", key: key); using var reply = await owner.SendAsync(req, ct); Assert.Equal(HttpStatusCode.OK, reply.StatusCode); }
        returns = (await owner.GetFromJsonAsync<TreasuryReturnPage>("/api/v2/me/treasury-returns", ct))!; Assert.Equal("40", Assert.Single(returns.Items).Amount);
        await Fund(owner, c, id, "-40"); await Command<TreasuryTransferResult>(owner, HttpMethod.Post, path + "/return");
        Assert.Empty((await owner.GetFromJsonAsync<TreasuryReturnPage>("/api/v2/me/treasury-returns", ct))!.Items);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(100, await db.TreasuryEntries.Where(x => x.Kind == "disband_return").SumAsync(x => x.Amount, ct)); Assert.Equal(0, (await db.Groups.SingleAsync(ct)).TreasuryOwned);
    }

    [Fact]
    public async Task Treasury_receipt_pages_and_account_scope_do_not_leak_to_other_groups_or_communities()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var id = (await Login(owner, "1270")).Character.Id; var c = await Create(owner); var group = await TreasuryGroup(owner, c); var second = await TreasuryGroup(owner, c, "Other"); var other = await Create(owner, "Separate economy");
        await Fund(owner, c, id, "100"); await TreasuryMove(owner, c, group, "60"); await TreasuryMove(owner, c, group, "10", false);
        var path = TreasuryPath(c, group); var page = (await owner.GetFromJsonAsync<TreasuryReceiptPage>(path + "/records?limit=1", ct))!; Assert.Single(page.Items); Assert.NotNull(page.NextCursor);
        var next = (await owner.GetFromJsonAsync<TreasuryReceiptPage>(path + "/records?limit=1&cursor=" + Uri.EscapeDataString(page.NextCursor!), ct))!; Assert.Single(next.Items); Assert.Null(next.NextCursor); Assert.NotEqual(page.Items[0].Id, next.Items[0].Id);
        using (var wrongCursor = await owner.GetAsync(TreasuryPath(c, second) + "/records?cursor=" + Uri.EscapeDataString(page.NextCursor!), ct)) Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCursor.StatusCode);
        using (var cross = Request(HttpMethod.Post, TreasuryPath(other, group) + "/withdrawals", new TreasuryTransfer("1")))
        using (var denied = await owner.SendAsync(cross, ct)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal("0", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(c, second), ct))!.Owned);
    }

    [Fact]
    public async Task Treasury_migration_keeps_profiles_character_balances_and_feature_approval_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var id = (await Login(owner, "1280")).Character.Id; var c = await Create(owner);
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Preserved", "Description")); await Fund(owner, c, id, "500");
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261029200422_GroupStaffPermissions", ct); db.ChangeTracker.Clear(); await db.Database.MigrateAsync(ct);
        var restored = await db.Groups.SingleAsync(ct); Assert.Equal(group.Id, restored.Id); Assert.Equal("Description", restored.Description); Assert.Equal(0, restored.TreasuryOwned); Assert.DoesNotContain(GroupCapabilities.Treasury, restored.EnabledFeatures);
        Assert.Equal(500, (await db.Memberships.SingleAsync(ct)).Owned); Assert.Empty(await db.TreasuryEntries.ToArrayAsync(ct)); Assert.False(db.Database.HasPendingModelChanges());
        group = await Command<GroupProfile>(owner, HttpMethod.Put, TreasuryPath(c, group)[..^9] + "/features", new GroupFeaturesEdit(GroupCapabilities.Features.ToArray()), group.Etag);
        await TreasuryMove(owner, c, group, "100"); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261029200422_GroupStaffPermissions", ct));
        Assert.Equal(100, (await db.Groups.SingleAsync(ct)).TreasuryOwned); Assert.Single(await db.TreasuryEntries.ToArrayAsync(ct));
    }
}
