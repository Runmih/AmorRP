using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Features.Billing;
using AmorRP.Server.Features.Groups;
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
    private static string BillPath(Community c, GroupProfile group) => $"/api/v2/communities/{c.Id}/groups/{group.Id}/bills";
    private async Task<GroupProfile> TrustBilling(HttpClient customer, Community c, GroupProfile group, bool trusted = true)
    {
        var path = $"/api/v2/communities/{c.Id}/groups/{group.Id}";
        var view = (await customer.GetFromJsonAsync<GroupProfile>(path, TestContext.Current.CancellationToken))!;
        var actor = (await customer.GetFromJsonAsync<Community>($"/api/v2/communities/{c.Id}", TestContext.Current.CancellationToken))!.MyBalance.CharacterId;
        return await Command<GroupProfile>(customer, HttpMethod.Put, path + "/trust", new GroupTrustEdit(trusted), GroupService.TrustEtag(group.Id, actor, view.TrustVersion));
    }
    private async Task DebtPolicy(HttpClient owner, Community c, bool allow)
    {
        var path = $"/api/v2/communities/{c.Id}/billing-policy";
        var policy = (await owner.GetFromJsonAsync<BillingPolicy>(path, TestContext.Current.CancellationToken))!;
        await Command<BillingPolicy>(owner, HttpMethod.Put, path, new BillingPolicyEdit(allow), policy.Etag);
    }
    private Task<Bill> Charge(HttpClient cashier, Community c, GroupProfile group, Guid customer, string amount, string service = "Service") =>
        Command<Bill>(cashier, HttpMethod.Post, BillPath(c, group), new BillCreate(Guid.CreateVersion7(), customer, amount, service));
    private async Task<Balance> OwnBalance(HttpClient client, Community c) =>
        (await client.GetFromJsonAsync<BalancePage>($"/api/v2/communities/{c.Id}/balances", TestContext.Current.CancellationToken))!.Items.Single();

    [Fact]
    public async Task Trusted_billing_requires_approval_customer_trust_and_individual_staff_authority()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1300"); var c = await Create(owner); var g = await TreasuryGroup(owner, c);
        using var payer = f.CreateClient(); var payerId = (await Login(payer, "1301")).Character.Id; await Join(payer, await Invite(owner, c)); await Fund(owner, c, payerId, "100");
        using (var request = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), payerId, "30", "Meal")))
        using (var response = await owner.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var trusted = await TrustBilling(payer, c, g); Assert.True(trusted.IsTrusted); Assert.False(trusted.IsMember);
        using var staff = f.CreateClient(); var staffId = (await Login(staff, "1302")).Character.Id; await Join(staff, await Invite(owner, c));
        await GroupAction(staff, HttpMethod.Post, TreasuryPath(c, g)[..^9] + "/join", new GroupJoin());
        using (var req = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), payerId, "30", "Meal")))
        using (var denied = await staff.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantStaff(owner, TreasuryPath(c, g)[..^9], staffId, GroupCapabilities.Billing);
        var bill = await Charge(staff, c, g, payerId, "30"); Assert.Equal("30", bill.Collected); Assert.Equal("0", bill.Outstanding); Assert.Equal("70", (await OwnBalance(payer, c)).Owned);
        await GrantStaff(owner, TreasuryPath(c, g)[..^9], staffId);
        clock.UtcNow = clock.UtcNow.AddSeconds(31);
        using (var req = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), payerId, "30", "Meal")))
        using (var denied = await staff.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await TrustBilling(payer, c, g, false);
        using var untrusted = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), payerId, "1", "Meal")); using var blocked = await owner.SendAsync(untrusted, ct); Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task Debt_disabled_rejects_unaffordable_bills_atomically_and_only_owner_can_change_policy()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1310"); var c = await Create(owner); var g = await TreasuryGroup(owner, c);
        using var payer = f.CreateClient(); var id = (await Login(payer, "1311")).Character.Id; await Join(payer, await Invite(owner, c)); await TrustBilling(payer, c, g); await Fund(owner, c, id, "30");
        var policy = (await owner.GetFromJsonAsync<BillingPolicy>($"/api/v2/communities/{c.Id}/billing-policy", ct))!; Assert.False(policy.AllowDebt);
        using (var req = Request(HttpMethod.Put, $"/api/v2/communities/{c.Id}/billing-policy", new BillingPolicyEdit(true), etag: policy.Etag))
        using (var denied = await payer.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var req = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), id, "100", "Room")))
        using (var denied = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal("30", (await OwnBalance(payer, c)).Owned); Assert.Equal("0", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(c, g), ct))!.Owned);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); Assert.Empty(await db.Bills.ToArrayAsync(ct)); Assert.Empty(await db.BillPayments.ToArrayAsync(ct));
    }

    [Fact]
    public async Task Offline_bill_collects_available_coins_and_new_income_reduces_oldest_debts_without_minting()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1320"); var c = await Create(owner); var a = await TreasuryGroup(owner, c, "A"); var b = await TreasuryGroup(owner, c, "B");
        Guid id;
        using (var payer = f.CreateClient()) { id = (await Login(payer, "1321")).Character.Id; await Join(payer, await Invite(owner, c)); await TrustBilling(payer, c, a); await TrustBilling(payer, c, b); }
        await DebtPolicy(owner, c, true); await Fund(owner, c, id, "30"); var first = await Charge(owner, c, a, id, "100", "Room"); Assert.Equal("30", first.Collected); Assert.Equal("70", first.Outstanding);
        clock.UtcNow = clock.UtcNow.AddSeconds(1); var second = await Charge(owner, c, b, id, "50", "Drink"); Assert.Equal("50", second.Outstanding);
        var balance = await Fund(owner, c, id, "90"); Assert.Equal("0", balance.Owned);
        await DebtPolicy(owner, c, false); // Existing debts continue despite the policy change.
        await Fund(owner, c, id, "40");
        using var returned = f.CreateClient(); await Login(returned, "1321"); var page = (await returned.GetFromJsonAsync<BillPage>($"/api/v2/communities/{c.Id}/bills/mine", ct))!;
        Assert.Equal("0", page.TotalOutstanding); Assert.All(page.Items, x => Assert.Equal("0", x.Outstanding)); Assert.Equal("10", (await OwnBalance(returned, c)).Owned);
        Assert.Equal("100", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(c, a), ct))!.Owned); Assert.Equal("50", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(c, b), ct))!.Owned);
        var payments = (await returned.GetFromJsonAsync<BillPaymentPage>($"/api/v2/communities/{c.Id}/bills/{first.Id}/payments", ct))!; Assert.Equal(2, payments.Items.Length); Assert.Equal(100, payments.Items.Sum(x => long.Parse(x.Amount)));
    }

    [Fact]
    public async Task Bills_preserve_trade_reservations_and_collect_coins_released_by_cancellation()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1330")).Character.Id; var c = await Create(owner); var g = await TreasuryGroup(owner, c); await DebtPolicy(owner, c, true);
        using var payer = f.CreateClient(); var id = (await Login(payer, "1331")).Character.Id; await Join(payer, await Invite(owner, c)); await TrustBilling(payer, c, g); await Fund(owner, c, id, "100");
        var trade = await Command<Trade>(payer, HttpMethod.Post, $"/api/v2/communities/{c.Id}/trades", new TradeCreate(ownerId)); trade = await TradeCommand(owner, c, trade, "accept"); trade = await Offer(payer, c, trade, "70");
        var bill = await Charge(owner, c, g, id, "100"); Assert.Equal("30", bill.Collected); Assert.Equal("70", (await OwnBalance(payer, c)).Reserved);
        await TradeCommand(payer, c, trade, "cancel"); var balance = await OwnBalance(payer, c); Assert.Equal("0", balance.Owned); Assert.Equal("0", balance.Reserved);
        Assert.Equal("0", (await payer.GetFromJsonAsync<BillPage>($"/api/v2/communities/{c.Id}/bills/mine", ct))!.TotalOutstanding);
    }

    [Fact]
    public async Task Trade_income_and_treasury_withdrawals_share_automatic_collection()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1340")).Character.Id; var c = await Create(owner); var g = await TreasuryGroup(owner, c); await DebtPolicy(owner, c, true);
        using var payer = f.CreateClient(); var id = (await Login(payer, "1341")).Character.Id; await Join(payer, await Invite(owner, c)); await TrustBilling(payer, c, g); await GroupAction(payer, HttpMethod.Post, TreasuryPath(c, g)[..^9] + "/join", new GroupJoin()); await GrantStaff(owner, TreasuryPath(c, g)[..^9], id, GroupCapabilities.TreasuryWithdraw);
        await Charge(owner, c, g, id, "100"); await Fund(owner, c, ownerId, "80");
        var trade = await Command<Trade>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/trades", new TradeCreate(id)); trade = await TradeCommand(payer, c, trade, "accept"); trade = await Offer(owner, c, trade, "50");
        foreach (var action in new[] { "confirm", "final-confirm" }) { trade = await TradeCommand(owner, c, trade, action); trade = await TradeCommand(payer, c, trade, action); }
        Assert.Equal("0", (await OwnBalance(payer, c)).Owned); Assert.Equal("50", (await payer.GetFromJsonAsync<BillPage>($"/api/v2/communities/{c.Id}/bills/mine", ct))!.TotalOutstanding);
        var withdrawal = await TreasuryMove(payer, c, g, "30", false); Assert.Equal("0", withdrawal.CharacterBalance.Owned); Assert.Equal("50", withdrawal.Treasury.Owned);
        Assert.Equal("20", (await payer.GetFromJsonAsync<BillPage>($"/api/v2/communities/{c.Id}/bills/mine", ct))!.TotalOutstanding);
    }

    [Fact]
    public async Task Bill_identity_survives_new_operation_keys_and_cooldown_prevents_accidental_second_charge()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1350"); var c = await Create(owner); var g = await TreasuryGroup(owner, c);
        using var payer = f.CreateClient(); var id = (await Login(payer, "1351")).Character.Id; await Join(payer, await Invite(owner, c)); await TrustBilling(payer, c, g); await Fund(owner, c, id, "100");
        var body = new BillCreate(Guid.CreateVersion7(), id, "30", "Lunch"); var key = Guid.CreateVersion7(clock.GetUtcNow()); string? first = null;
        for (var i = 0; i < 3; i++)
        {
            using var req = Request(HttpMethod.Post, BillPath(c, g), body, i < 2 ? key : null); using var response = await owner.SendAsync(req, ct); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync(ct); if (i == 0) first = json; if (i == 1) Assert.Equal(first, json);
        }
        using (var req = Request(HttpMethod.Post, BillPath(c, g), body with { BillId = Guid.CreateVersion7() })) using (var response = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using (var req = Request(HttpMethod.Post, BillPath(c, g), body with { Amount = "1" })) using (var response = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("70", (await OwnBalance(payer, c)).Owned);
        clock.UtcNow = clock.UtcNow.AddDays(8); await Login(owner, "1350");
        await Command<Bill>(owner, HttpMethod.Post, BillPath(c, g), body); // Stable identity outlives protected-operation replay.
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); Assert.Single(await db.Bills.ToArrayAsync(ct)); Assert.Single(await db.BillPayments.ToArrayAsync(ct));
    }

    [Fact]
    public async Task Disbanded_claims_remain_collectable_as_former_owner_returns_without_automatic_cycles()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1360")).Character.Id; var c = await Create(owner); var g = await TreasuryGroup(owner, c); await DebtPolicy(owner, c, true);
        await TrustBilling(owner, c, g); var bill = await Charge(owner, c, g, ownerId, "100");
        await Command<GroupProfile>(owner, HttpMethod.Delete, TreasuryPath(c, g)[..^9], etag: g.Etag);
        await Fund(owner, c, ownerId, "1");
        var returns = (await owner.GetFromJsonAsync<TreasuryReturnPage>("/api/v2/me/treasury-returns", ct))!; Assert.Equal("1", Assert.Single(returns.Items).Amount);
        await Command<TreasuryTransferResult>(owner, HttpMethod.Post, TreasuryPath(c, g) + "/return");
        var bills = (await owner.GetFromJsonAsync<BillPage>($"/api/v2/communities/{c.Id}/bills/mine", ct))!; Assert.Equal("98", bills.TotalOutstanding);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); Assert.Equal(1, (await db.Groups.SingleAsync(ct)).TreasuryOwned); Assert.Equal(0, (await db.Memberships.SingleAsync(ct)).Owned);
        Assert.Equal(2, await db.BillPayments.CountAsync(ct));
    }

    [Fact]
    public async Task Trust_departure_and_financial_reads_do_not_grant_cross_group_or_moderator_private_access()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var admin = f.CreateClient(); await Login(admin, "1370"); var c = await Create(admin);
        using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1371")).Character.Id; var invitation = await Invite(admin, c); await Join(owner, invitation); await GrantHome(admin, c, ownerId, "groups.create");
        var g = await Command<GroupProfile>(owner, HttpMethod.Post, $"/api/v2/communities/{c.Id}/groups", new GroupWrite("Private", "")); g = await Command<GroupProfile>(admin, HttpMethod.Put, TreasuryPath(c, g)[..^9] + "/features", new GroupFeaturesEdit(GroupCapabilities.Features.ToArray()), g.Etag);
        using var payer = f.CreateClient(); var id = (await Login(payer, "1372")).Character.Id; await Join(payer, invitation); await TrustBilling(payer, c, g); await Fund(admin, c, id, "40"); var bill = await Charge(owner, c, g, id, "10");
        using (var denied = await admin.GetAsync(BillPath(c, g), ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await admin.GetAsync($"/api/v2/communities/{c.Id}/bills/{bill.Id}/payments", ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Single((await payer.GetFromJsonAsync<BillPaymentPage>($"/api/v2/communities/{c.Id}/bills/{bill.Id}/payments", ct))!.Items);
        var member = await Member(admin, c, id); await Command<Member>(admin, HttpMethod.Delete, $"/api/v2/communities/{c.Id}/members/{id}", new MemberRemoval("Left"), member.Etag); await Join(payer, invitation);
        Assert.False((await payer.GetFromJsonAsync<GroupProfile>(TreasuryPath(c, g)[..^9], ct))!.IsTrusted);
        using var req = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), id, "1", "Room")); using var deniedBill = await owner.SendAsync(req, ct); Assert.Equal(HttpStatusCode.Forbidden, deniedBill.StatusCode);
    }

    [Fact]
    public async Task Long_repayment_queue_holds_funds_until_durable_continuation_finishes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var ownerId = (await Login(owner, "1380")).Character.Id; var c = await Create(owner); var g = await TreasuryGroup(owner, c);
        using var payer = f.CreateClient(); var id = (await Login(payer, "1381")).Character.Id; await Join(payer, await Invite(owner, c));
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            db.Bills.AddRange(Enumerable.Range(0, DebtSettlement.BatchSize + 1).Select(i => new BillRow { Id = Guid.CreateVersion7(), CommunityId = c.Id, GroupId = g.Id, IssuerCharacterId = ownerId, CharacterId = id, Amount = 1, Service = "Seeded unpaid bill", RequestHash = "seed", CreatedAt = clock.GetUtcNow().AddSeconds(i) })); await db.SaveChangesAsync(ct);
        }
        await GroupAction(payer, HttpMethod.Post, TreasuryPath(c, g)[..^9] + "/join", new GroupJoin());
        var balance = await Fund(owner, c, id, (DebtSettlement.BatchSize + 1).ToString()); Assert.Equal("1", balance.HeldForDebt); Assert.Equal("0", balance.Available);
        using (var req = Request(HttpMethod.Post, TreasuryPath(c, g) + "/deposits", new TreasuryTransfer("1"))) using (var denied = await payer.SendAsync(req, ct)) Assert.False(denied.IsSuccessStatusCode);
        using var restarted = Factory();
        await using (var scope = restarted.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); await using var tx = await db.Database.BeginTransactionAsync(ct); await CommandStore.LockAsync(db, ["community:" + c.Id], ct);
            var member = await db.Memberships.SingleAsync(x => x.CommunityId == c.Id && x.CharacterId == id, ct); Assert.True(member.CollectionPending);
            await scope.ServiceProvider.GetRequiredService<DebtSettlement>().CollectAsync(member, "automatic", null, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            Assert.False(member.CollectionPending); Assert.Equal(0, member.Owned); Assert.Equal(DebtSettlement.BatchSize + 1, await db.BillPayments.CountAsync(ct));
        }
    }

    [Fact]
    public async Task Concurrent_cashiers_cannot_overcharge_a_debt_disabled_balance()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1385"); var c = await Create(owner); var g = await TreasuryGroup(owner, c);
        using var staff = f.CreateClient(); var staffId = (await Login(staff, "1386")).Character.Id; await Join(staff, await Invite(owner, c)); await GroupAction(staff, HttpMethod.Post, TreasuryPath(c, g)[..^9] + "/join", new GroupJoin()); await GrantStaff(owner, TreasuryPath(c, g)[..^9], staffId, GroupCapabilities.Billing);
        using var payer = f.CreateClient(); var id = (await Login(payer, "1387")).Character.Id; await Join(payer, await Invite(owner, c)); await TrustBilling(payer, c, g); await Fund(owner, c, id, "100");
        using var first = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), id, "80", "First")); using var second = Request(HttpMethod.Post, BillPath(c, g), new BillCreate(Guid.CreateVersion7(), id, "80", "Second"));
        var replies = await Task.WhenAll(owner.SendAsync(first, ct), staff.SendAsync(second, ct)); Assert.Single(replies, x => x.IsSuccessStatusCode); Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var response in replies) response.Dispose();
        Assert.Equal("20", (await OwnBalance(payer, c)).Owned); Assert.Equal("80", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(c, g), ct))!.Owned);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); Assert.Single(await db.Bills.ToArrayAsync(ct));
    }

    [Fact]
    public async Task Billing_migration_preserves_money_and_starts_without_trust_or_debt_authorization()
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); var id = (await Login(owner, "1390")).Character.Id; var c = await Create(owner); var g = await TreasuryGroup(owner, c); await Fund(owner, c, id, "100"); await TreasuryMove(owner, c, g, "30");
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261029204527_GroupTreasury", ct); db.ChangeTracker.Clear(); await db.Database.MigrateAsync(ct);
        Assert.False((await db.Communities.SingleAsync(ct)).AllowDebt); Assert.Empty(await db.GroupTrusts.ToArrayAsync(ct)); Assert.Equal(70, (await db.Memberships.SingleAsync(ct)).Owned); Assert.Equal(30, (await db.Groups.SingleAsync(ct)).TreasuryOwned); Assert.False(db.Database.HasPendingModelChanges());
    }
}
