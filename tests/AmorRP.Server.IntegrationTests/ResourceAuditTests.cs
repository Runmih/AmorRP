using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Ownership_transfer_cannot_reset_the_creators_budget_or_charge_the_recipient()
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("ServicePolicy:OwnedCommunities", "1");
            b.UseSetting("ServicePolicy:MaxRetainedCreatedCommunitiesPerCharacter", "1");
        });
        await Migrate(factory); using var owner = factory.CreateClient(); var creator = await Login(owner, "753");
        using var member = factory.CreateClient(); var recipient = await Login(member, "754");
        var community = await Create(owner); await Join(member, await Invite(owner, community));
        var transfer = await Command<OwnershipTransfer>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/ownership-transfers", new OwnershipProposal(recipient.Character.Id));
        await Command<OwnershipTransfer>(member, HttpMethod.Post, $"/api/v2/communities/{community.Id}/ownership-transfers/{transfer.Id}/accept");
        using var create = Request(HttpMethod.Post, "/api/v2/communities", new CommunityCreate("Bypass", "", "Coins", "", 0, 5));
        using var rejected = await owner.SendAsync(create, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("community_retention_limit_reached", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await DeleteResourceCommunity(member, community); await Create(member, "Recipient's own community");
        await using var scope = factory.Services.CreateAsyncScope();
        var retained = await scope.ServiceProvider.GetRequiredService<AmorDbContext>().Communities.SingleAsync(x => x.Id == community.Id, TestContext.Current.CancellationToken);
        Assert.Equal(creator.Character.Id, retained.CreatedByCharacterId); Assert.Equal(recipient.Character.Id, retained.OwnerCharacterId);
    }

    [Fact]
    public async Task Concurrent_creations_cannot_exceed_retained_budget_and_same_key_replay_still_succeeds()
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("ServicePolicy:OwnedCommunities", "2");
            b.UseSetting("ServicePolicy:MaxRetainedCreatedCommunitiesPerCharacter", "2");
        });
        await Migrate(factory); using var client = factory.CreateClient(); await Login(client, "755");
        await DeleteResourceCommunity(client, await Create(client));
        var body = new CommunityCreate("Final slot", "", "Coins", "", 0, 5);
        var keys = new[] { Guid.CreateVersion7(clock.GetUtcNow()), Guid.CreateVersion7(clock.GetUtcNow()) };
        var responses = await Task.WhenAll(keys.Select(async key =>
        {
            using var request = Request(HttpMethod.Post, "/api/v2/communities", body, key);
            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }));
        try
        {
            var accepted = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            var rejected = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains("community_retention_limit_reached", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var original = (await accepted.Content.ReadFromJsonAsync<CommandResult<Community>>(TestContext.Current.CancellationToken))!;
            using var retry = Request(HttpMethod.Post, "/api/v2/communities", body, keys[Array.IndexOf(responses, accepted)]);
            using var replay = await client.SendAsync(retry, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal(original.OperationId, (await replay.Content.ReadFromJsonAsync<CommandResult<Community>>(TestContext.Current.CancellationToken))!.OperationId);
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AmorDbContext>().Communities.CountAsync(TestContext.Current.CancellationToken));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Creator_budget_upgrade_recovers_original_creator_and_preserves_legacy_records()
    {
        using var factory = Factory(); await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261015191138_LoginNetworkAdmission", ct);
        var creator = Guid.NewGuid(); var owner = Guid.NewGuid(); var community = Guid.NewGuid(); var legacy = Guid.NewGuid();
        var currency = Guid.NewGuid(); var legacyCurrency = Guid.NewGuid(); var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt") VALUES
                ({creator}, '756', 'creator-binding', 'Creator', '35', 'Zalera', {now}),
                ({owner}, '757', 'owner-binding', 'Owner', '35', 'Zalera', {now});
            INSERT INTO groups ("Id", "Name", "Description", "OwnerCharacterId", "Version", "Deleted", "CurrencyId", "CurrencyName", "CurrencySymbol", "CurrencyVersion", "PolicyVersion", "CurrentPotionPoints", "CurrentLetters", "NextPotionPoints", "NextLetters", "PolicyPeriod", "InventoryVersion") VALUES
                ({community}, 'Transferred and deleted', '', {owner}, 9, true, {currency}, 'Coins', 'C', 1, 1, 12, 5, 12, 5, {now}, 4),
                ({legacy}, 'Legacy without receipt', '', {owner}, 3, false, {legacyCurrency}, 'Coins', 'C', 1, 1, 12, 5, 12, 5, {now}, 2);
            """, ct);
        await LegacyInsert(db, new OperationRow { Scope = "character:" + creator, Key = Guid.NewGuid(), ActorCharacterId = creator, CommunityId = community,
            Kind = "community.create", OccurredAt = now, ProtectedResponse = "retained receipt" }, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO memberships ("GroupId", "CharacterId", "Status", "Capabilities", "TradingRestricted", "Version", "Owned", "Reserved", "BalanceVersion")
            VALUES ({legacy}, {owner}, 'active', ARRAY[]::text[], false, 1, 42, 0, 1);
            """, ct);
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); await db.Database.MigrateAsync(ct);
        var upgraded = await db.Communities.SingleAsync(x => x.Id == community, ct);
        Assert.Equal(creator, upgraded.CreatedByCharacterId); Assert.Equal(owner, upgraded.OwnerCharacterId);
        Assert.True(upgraded.Deleted); Assert.Equal(9, upgraded.Version); Assert.Equal(currency, upgraded.CurrencyId);
        var fallback = await db.Communities.SingleAsync(x => x.Id == legacy, ct);
        Assert.Equal(owner, fallback.CreatedByCharacterId); Assert.False(fallback.Deleted);
        Assert.Equal(42, (await db.Memberships.SingleAsync(ct)).Owned);
        Assert.Equal("", (await db.Memberships.SingleAsync(ct)).Description);
        Assert.Empty((await db.Memberships.SingleAsync(ct)).ReadAnnouncementIds);
        Assert.Equal(0, upgraded.AnnouncementSequence);
        Assert.Empty(await db.Announcements.ToArrayAsync(ct));
        Assert.Equal("retained receipt", (await db.Operations.SingleAsync(ct)).ProtectedResponse);
    }

    [Fact]
    public async Task Create_delete_cycles_do_not_reset_the_retained_community_budget()
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("ServicePolicy:OwnedCommunities", "1");
            b.UseSetting("ServicePolicy:MaxRetainedCreatedCommunitiesPerCharacter", "2");
        });
        await Migrate(factory); using var client = factory.CreateClient(); await Login(client, "750");
        for (var i = 0; i < 2; i++) await DeleteResourceCommunity(client, await Create(client, "Retained " + i));
        using var create = Request(HttpMethod.Post, "/api/v2/communities", new CommunityCreate("Another", "", "Coins", "", 0, 5));
        using var response = await client.SendAsync(create, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("community_retention_limit_reached", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(2, await db.Communities.CountAsync(TestContext.Current.CancellationToken));
        Assert.All(await db.Communities.ToArrayAsync(TestContext.Current.CancellationToken), row => Assert.True(row.Deleted));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repeated_approval_with_a_new_key_does_not_add_consent_but_same_key_recovery_still_works(bool final)
    {
        using var factory = Factory(); await Migrate(factory); using var owner = factory.CreateClient(); var identity = await Login(owner, "751");
        var community = await Create(owner); using var member = factory.CreateClient(); var other = await Login(member, "752");
        await Join(member, await Invite(owner, community));
        await Command<Balance>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/currency/adjustments", new CurrencyAdjustment(identity.Character.Id, "10", "Seed"));
        var trade = await Command<Trade>(owner, HttpMethod.Post, $"/api/v2/communities/{community.Id}/trades", new TradeCreate(other.Character.Id));
        trade = await TradeCommand(member, community, trade, "accept"); trade = await Offer(owner, community, trade, "4");
        if (final) { trade = await TradeCommand(owner, community, trade, "confirm"); trade = await TradeCommand(member, community, trade, "confirm"); }
        var action = final ? "final-confirm" : "confirm";
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var body = new TradeRevision(trade.Revision);
        var path = $"/api/v2/communities/{community.Id}/trades/{trade.Id}/{action}";
        using var first = Request(HttpMethod.Post, path, body, key);
        using var accepted = await owner.SendAsync(first, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var consent = (await accepted.Content.ReadFromJsonAsync<CommandResult<Trade>>(TestContext.Current.CancellationToken))!;
        trade = consent.Result;
        using var duplicate = Request(HttpMethod.Post, path, body);
        using var rejected = await owner.SendAsync(duplicate, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("trade_already_confirmed", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var retry = Request(HttpMethod.Post, path, body, key);
        using var recovered = await owner.SendAsync(retry, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(consent.OperationId, (await recovered.Content.ReadFromJsonAsync<CommandResult<Trade>>(TestContext.Current.CancellationToken))!.OperationId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
            Assert.Equal(1, await db.Operations.CountAsync(x => x.TradeId == trade.Id && x.ActorCharacterId == identity.Character.Id && x.Kind == "trade." + action, TestContext.Current.CancellationToken));
        }
        if (!final)
        {
            trade = await TradeCommand(member, community, trade, "confirm");
            trade = await TradeCommand(owner, community, trade, "final-confirm");
        }
        trade = await TradeCommand(member, community, trade, "final-confirm"); Assert.Equal("completed", trade.State);
    }

    [Fact]
    public async Task Public_readiness_bursts_do_not_multiply_database_checks()
    {
        var counter = new ReadinessQueryCounter();
        using var factory = Factory().WithWebHostBuilder(b => b.ConfigureServices(services =>
            services.ConfigureDbContext<AmorDbContext>(options => options.AddInterceptors(counter))));
        await Migrate(factory); using var client = factory.CreateClient();
        await WaitForResourceReadiness(client, HttpStatusCode.OK);
        counter.Start();
        var responses = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => client.GetAsync("/health/ready", TestContext.Current.CancellationToken)));
        var checks = counter.Stop();
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses) response.Dispose();
        Assert.InRange(checks, 0, 1);
        using var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    private async Task DeleteResourceCommunity(HttpClient client, Community community)
    {
        using var current = await client.GetAsync($"/api/v2/communities/{community.Id}", TestContext.Current.CancellationToken);
        using var deletion = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/deletion", new CommunityDeletion(community.Name), etag: current.Headers.ETag!.ToString());
        using var response = await client.SendAsync(deletion, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task WaitForResourceReadiness(HttpClient client, HttpStatusCode status)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            using var response = await client.GetAsync("/health/ready", timeout.Token);
            if (response.StatusCode == status) return;
            await Task.Delay(25, timeout.Token);
        }
    }
}

internal sealed class ReadinessQueryCounter : DbCommandInterceptor
{
    private int armed;
    private int count;
    public void Start() { count = 0; Volatile.Write(ref armed, 1); }
    public int Stop() { Volatile.Write(ref armed, 0); return count; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref armed) == 1 && command.CommandText.Contains("FROM infrastructure_state AS", StringComparison.Ordinal)) Interlocked.Increment(ref count);
        return ValueTask.FromResult(result);
    }
}
