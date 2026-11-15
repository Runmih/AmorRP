using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private WebApplicationFactory<Program> SeasonFactory(bool configured = true)
    {
        seasonPolicy.AdministratorLodestoneIds = configured ? ["90001"] : [];
        return Factory();
    }
    private async Task<SeasonSummary> Transition(HttpClient c, Season season, bool activate = true)
    {
        var latest = (await c.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!.Seasons.Single(x => x.Id == season.Id);
        return await Command<SeasonSummary>(c, HttpMethod.Post, $"/api/v2/seasons/{season.Id}/{(activate ? "activation" : "archive")}", new SeasonTransition(season.Name), $"\"season:{season.Id:N}:{latest.Version}\"");
    }
    private Task<SeasonPage?> SeasonList(HttpClient c) => c.GetFromJsonAsync<SeasonPage>("/api/v2/seasons", TestContext.Current.CancellationToken);

    [Fact]
    public async Task Seasons_require_configured_verified_administrator_and_never_bootstrap_the_first_player()
    {
        using var f = SeasonFactory(); await Migrate(f);
        using var ordinary = f.CreateClient(); await Login(ordinary, "90002");
        Assert.False((await ordinary.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!.IsAdministrator);
        using (var create = Request(HttpMethod.Post, "/api/v2/seasons", new SeasonCreate("Forbidden", "", "Coins", "C", 0, 5)))
        {
            create.Headers.Add("X-Lodestone-Id", "90001");
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.SendAsync(create, TestContext.Current.CancellationToken)).StatusCode);
        }
        using var admin = f.CreateClient(); await Login(admin, "90001");
        Assert.True((await admin.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!.IsAdministrator);
        var draft = await Draft(admin, "First season"); Assert.Equal("draft", draft.SeasonState);
        Assert.Empty((await SeasonList(ordinary))!.Items);
        using (var activate = Request(HttpMethod.Post, $"/api/v2/seasons/{draft.Id}/activation", new SeasonTransition(draft.Name), etag: $"\"season:{draft.Id:N}:1\""))
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.SendAsync(activate, TestContext.Current.CancellationToken)).StatusCode);
        using var empty = SeasonFactory(configured: false); using var noAdmin = empty.CreateClient(); await Login(noAdmin, "90001");
        using var denied = Request(HttpMethod.Post, "/api/v2/seasons", new SeasonCreate("No administrator", "", "Coins", "C", 0, 5));
        Assert.Equal(HttpStatusCode.Forbidden, (await noAdmin.SendAsync(denied, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Activation_enrolls_existing_and_future_players_once_and_preserves_moderation()
    {
        using var f = SeasonFactory(); await Migrate(f);
        using var admin = f.CreateClient(); await Login(admin, "90001");
        using var playerClient = f.CreateClient(); var playerIdentity = await Login(playerClient, "90003");
        var season = await Draft(admin); await Transition(admin, season);
        Assert.Equal(season.Id, Assert.Single((await SeasonList(playerClient))!.Items).Id);
        using var late = f.CreateClient(); var lateIdentity = await Login(late, "90004");
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SeasonList(late)));
        var member = await Member(admin, season, lateIdentity.Character.Id);
        await Command<Member>(admin, HttpMethod.Put, $"/api/v2/seasons/{season.Id}/members/{lateIdentity.Character.Id}/block", new MemberRemoval("Test moderation"), member.Etag);
        Assert.Empty((await SeasonList(late))!.Items);
        Assert.Empty((await SeasonList(late))!.Items);
        var blocked = await Member(admin, season, lateIdentity.Character.Id);
        await Command<Member>(admin, HttpMethod.Post, $"/api/v2/seasons/{season.Id}/members/{lateIdentity.Character.Id}/restoration", new MemberRemoval("Restored explicitly"), blocked.Etag);
        Assert.Single((await SeasonList(late))!.Items);
        var removed = await Member(admin, season, playerIdentity.Character.Id);
        await Command<Member>(admin, HttpMethod.Delete, $"/api/v2/seasons/{season.Id}/members/{playerIdentity.Character.Id}", new MemberRemoval("Remove from season"), removed.Etag);
        Assert.Empty((await SeasonList(playerClient))!.Items);
        var dormant = await Member(admin, season, playerIdentity.Character.Id);
        await Command<Member>(admin, HttpMethod.Post, $"/api/v2/seasons/{season.Id}/members/{playerIdentity.Character.Id}/restoration", new MemberRemoval("Welcome back"), dormant.Etag);
        Assert.Single((await SeasonList(playerClient))!.Items);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(3, await db.Memberships.CountAsync(x => x.CommunityId == season.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task New_season_keeps_archive_assets_private_and_separate_and_cancels_open_trades()
    {
        using var f = SeasonFactory(); await Migrate(f); using var admin = f.CreateClient(); var identity = await Login(admin, "90001");
        using var other = f.CreateClient(); var otherIdentity = await Login(other, "90005");
        var first = await Draft(admin, "Spring"); await Transition(admin, first);
        var recipe = await Recipe(admin, first);
        var potion = (await Command<ProductionResult>(admin, HttpMethod.Post, $"/api/v2/seasons/{first.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 2))).Holding;
        var category = (await admin.GetFromJsonAsync<CategoryPage>($"/api/v2/seasons/{first.Id}/categories", TestContext.Current.CancellationToken))!.Items.Single(x => x.AllowedTypeIds.Contains("letter"));
        var writing = (await Command<ProductionResult>(admin, HttpMethod.Post, $"/api/v2/seasons/{first.Id}/inventory/letters", new LetterCreate(category.Id, "Old letter", "Private archived text"))).Holding;
        var group = await Command<AmorRP.Contracts.Groups.Group>(admin, HttpMethod.Post, $"/api/v2/seasons/{first.Id}/groups", new GroupWrite("Spring guild", ""));
        var adjustment = new CurrencyAdjustment(identity.Character.Id, "100", "Season grant"); var adjustmentKey = Guid.CreateVersion7(clock.GetUtcNow());
        using (var command = Request(HttpMethod.Post, $"/api/v2/seasons/{first.Id}/currency/adjustments", adjustment, adjustmentKey))
            Assert.Equal(HttpStatusCode.OK, (await admin.SendAsync(command, TestContext.Current.CancellationToken)).StatusCode);
        var trade = await Command<Trade>(admin, HttpMethod.Post, $"/api/v2/seasons/{first.Id}/trades", new TradeCreate(otherIdentity.Character.Id));
        trade = await TradeCommand(other, first, trade, "accept");
        trade = await Offer(admin, first, trade, "30", new TradeLine(potion.Id, 1));
        var next = await Draft(admin, "Summer"); await Transition(admin, next);
        var list = (await SeasonList(admin))!.Items;
        Assert.Equal("archived", list.Single(x => x.Id == first.Id).SeasonState);
        Assert.Equal("100", list.Single(x => x.Id == first.Id).MyBalance.Owned);
        Assert.Equal("0", list.Single(x => x.Id == first.Id).MyBalance.Reserved);
        Assert.Equal("0", list.Single(x => x.Id == next.Id).MyBalance.Owned);
        Assert.NotEqual(first.Currency.Id, next.Currency.Id);
        Assert.Equal("cancelled", (await ReadTrade(admin, first, trade)).State);
        Assert.Equal(0, (await CurrentHolding(admin, first, potion.Id)).Reserved);
        Assert.Equal("Private archived text", (await admin.GetFromJsonAsync<HoldingDetails>($"/api/v2/seasons/{first.Id}/inventory/{writing.Id}", TestContext.Current.CancellationToken))!.Letter!.Body);
        Assert.Empty((await admin.GetFromJsonAsync<HoldingPage>($"/api/v2/seasons/{next.Id}/inventory", TestContext.Current.CancellationToken))!.Items);
        using (var change = Request(HttpMethod.Post, $"/api/v2/seasons/{first.Id}/currency/adjustments", adjustment))
            Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(change, TestContext.Current.CancellationToken)).StatusCode);
        var archivedHolding = await CurrentHolding(admin, first, potion.Id);
        using (var discard = Request(HttpMethod.Post, $"/api/v2/seasons/{first.Id}/inventory/{potion.Id}/discard", new DiscardRequest(1, archivedHolding.Version)))
            Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(discard, TestContext.Current.CancellationToken)).StatusCode);
        using (var create = Request(HttpMethod.Post, $"/api/v2/seasons/{first.Id}/groups", new GroupWrite("Archived new group", "")))
            Assert.Equal(HttpStatusCode.Conflict, (await admin.SendAsync(create, TestContext.Current.CancellationToken)).StatusCode);
        // Durable retries recover the old receipt without applying a second charge.
        using (var retry = Request(HttpMethod.Post, $"/api/v2/seasons/{first.Id}/currency/adjustments", adjustment, adjustmentKey))
        {
            using var replay = await admin.SendAsync(retry, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        }
        using (var cross = Request(HttpMethod.Post, $"/api/v2/seasons/{next.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 1)))
            Assert.Equal(HttpStatusCode.NotFound, (await admin.SendAsync(cross, TestContext.Current.CancellationToken)).StatusCode);
        using var newcomer = f.CreateClient(); await Login(newcomer, "90006"); Assert.Single((await SeasonList(newcomer))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await newcomer.GetAsync($"/api/v2/seasons/{first.Id}", TestContext.Current.CancellationToken)).StatusCode);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.False((await db.Groups.SingleAsync(x => x.Id == group.Id, TestContext.Current.CancellationToken)).Deleted);
        Assert.Equal("100", (await SeasonList(admin))!.Items.Single(x => x.Id == first.Id).MyBalance.Owned);
    }

    [Fact]
    public async Task Archived_pending_debt_stays_frozen_while_active_collection_continues()
    {
        using var f = SeasonFactory(); await Migrate(f); using var admin = f.CreateClient(); var identity = await Login(admin, "90001");
        var previous = await Draft(admin, "Archive"); await Transition(admin, previous);
        var current = await Draft(admin, "Current"); await Transition(admin, current);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var rows = await db.Memberships.Where(x => x.CharacterId == identity.Character.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        foreach (var row in rows) { row.CollectionPending = true; row.LastCollectionAttempt = 0; }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline && await db.Memberships.AsNoTracking().AnyAsync(x => x.CommunityId == current.Id && x.CollectionPending, TestContext.Current.CancellationToken))
            await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.False((await db.Memberships.AsNoTracking().SingleAsync(x => x.CommunityId == current.Id, TestContext.Current.CancellationToken)).CollectionPending);
        var archived = await db.Memberships.AsNoTracking().SingleAsync(x => x.CommunityId == previous.Id, TestContext.Current.CancellationToken);
        Assert.True(archived.CollectionPending); Assert.Equal(0, archived.LastCollectionAttempt);
    }

    [Fact]
    public async Task Concurrent_activation_is_serialized_and_stale_requests_and_reopening_archives_fail()
    {
        using var f = SeasonFactory(); await Migrate(f); using var admin = f.CreateClient(); await Login(admin, "90001");
        var first = await Draft(admin, "One"); var second = await Draft(admin, "Two");
        var activateKey = Guid.CreateVersion7(clock.GetUtcNow());
        async Task<HttpResponseMessage> Activate(Season s, Guid? key = null, int version = 1)
        {
            using var request = Request(HttpMethod.Post, $"/api/v2/seasons/{s.Id}/activation", new SeasonTransition(s.Name), key, $"\"season:{s.Id:N}:{version}\"");
            return await admin.SendAsync(request, TestContext.Current.CancellationToken);
        }
        var results = await Task.WhenAll(Activate(first, activateKey), Activate(second));
        foreach (var result in results) { Assert.Equal(HttpStatusCode.OK, result.StatusCode); result.Dispose(); }
        var context = (await admin.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!;
        Assert.Single(context.Seasons, x => x.State == "active"); var archived = Assert.Single(context.Seasons, x => x.State == "archived");
        using var stale = await Activate(first); Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var reopen = await Activate(archived.Id == first.Id ? first : second, version: archived.Version); Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
        using var replay = await Activate(first, activateKey); Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        var current = context.ActiveSeasonId == first.Id ? first : second; await Transition(admin, current, activate: false);
        Assert.Null((await admin.GetFromJsonAsync<SeasonContext>("/api/v2/seasons/context", TestContext.Current.CancellationToken))!.ActiveSeasonId);
    }

    [Fact]
    public async Task Historical_seasons_do_not_block_active_membership()
    {
        using var f = SeasonFactory(); await Migrate(f); using var admin = f.CreateClient(); await Login(admin, "90001");
        for (var i = 0; i < 8; i++) await Transition(admin, await Draft(admin, "Season " + i));
        Assert.Equal(8, (await SeasonList(admin))!.Items.Length);
        Assert.Single((await SeasonList(admin))!.Items, x => x.SeasonState == "active");
    }
}
