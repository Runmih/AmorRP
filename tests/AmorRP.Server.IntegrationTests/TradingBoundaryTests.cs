using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Trade_invitation_race_claims_both_participants_once_and_cross_group_targets_are_rejected()
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); await Login(a, "410"); var g = await Create(a);
        using var b = f.CreateClient(); var bb = await Login(b, "411"); await Join(b, await Invite(a, g));
        using var other = f.CreateClient(); var outsider = await Login(other, "412"); await Create(other);
        using var foreign = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(outsider.Character.Id)); Assert.Equal(HttpStatusCode.NotFound, (await a.SendAsync(foreign, TestContext.Current.CancellationToken)).StatusCode);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => { using var request = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); return await a.SendAsync(request, TestContext.Current.CancellationToken); }));
        Assert.Single(responses, x => x.IsSuccessStatusCode); Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict)); foreach (var response in responses) response.Dispose();
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(2, await db.TradeSlots.CountAsync(TestContext.Current.CancellationToken)); Assert.Single(await db.Trades.ToArrayAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Overflow_rejects_entire_exchange_and_correcting_offer_requires_new_trade()
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); var aa = await Login(a, "413"); var g = await Create(a); using var b = f.CreateClient(); var bb = await Login(b, "414"); await Join(b, await Invite(a, g));
        await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(aa.Character.Id, "10", "Seed"));
        await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(bb.Character.Id, long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), "Boundary"));
        var t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "1"); t = await TradeCommand(a, g, t, "confirm");
        t = await TradeCommand(b, g, t, "confirm"); t = await TradeCommand(a, g, t, "final-confirm");
        using var request = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/final-confirm", new TradeRevision(t.Revision)); Assert.Equal(HttpStatusCode.Conflict, (await b.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
        var current = await ReadTrade(a, g, t); Assert.Equal("confirming", current.State); Assert.True(current.Initiator.FinalConfirmed); Assert.False(current.Recipient.FinalConfirmed);
        var ga = (await a.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!; Assert.Equal("10", ga.MyBalance.Owned); Assert.Equal("1", ga.MyBalance.Reserved);
        t = await Offer(b, g, current, "1"); Assert.Equal("cancelled", t.State);
        t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept");
        t = await Offer(a, g, t, "1"); t = await Offer(b, g, t, "1");
        t = await TradeCommand(a, g, t, "confirm"); t = await TradeCommand(b, g, t, "confirm");
        t = await TradeCommand(a, g, t, "final-confirm"); t = await TradeCommand(b, g, t, "final-confirm"); Assert.Equal("completed", t.State);
        Assert.Equal("10", (await a.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!.MyBalance.Owned);
        Assert.Equal(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), (await b.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!.MyBalance.Owned);
    }
    [Fact]
    public async Task Full_inventory_writing_swap_uses_net_capacity_and_locks_both_documents()
    {
        using var f = Factory().WithWebHostBuilder(x => x.UseSetting("ServicePolicy:MaxHoldingsPerCharacterGroup", "1")); await Migrate(f);
        using var a = f.CreateClient(); var aa = await Login(a, "415"); var g = await Create(a); using var b = f.CreateClient(); var bb = await Login(b, "416"); await Join(b, await Invite(a, g));
        var category = (await a.GetFromJsonAsync<CategoryPage>($"/api/v1/groups/{g.Id}/categories", TestContext.Current.CancellationToken))!.Items.Single(x => x.Name == "Books");
        var book = (await Command<ProductionResult>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/inventory/letters", new LetterCreate(category.Id, "Book", "Book text", "book"))).Holding;
        var note = (await Command<ProductionResult>(b, HttpMethod.Post, $"/api/v1/groups/{g.Id}/inventory/letters", new LetterCreate(category.Id, "Note", "Note text", "note"))).Holding;
        var t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "0", new TradeLine(book.Id, 1)); t = await Offer(b, g, t, "0", new TradeLine(note.Id, 1));
        using var confirmsA = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/confirm", new TradeRevision(t.Revision)); using var confirmsB = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/confirm", new TradeRevision(t.Revision));
        var responses = await Task.WhenAll(a.SendAsync(confirmsA, TestContext.Current.CancellationToken), b.SendAsync(confirmsB, TestContext.Current.CancellationToken)); Assert.All(responses, x => Assert.True(x.IsSuccessStatusCode)); foreach (var response in responses) response.Dispose();
        t = await ReadTrade(a, g, t); Assert.Equal("confirming", t.State);
        t = await TradeCommand(a, g, t, "final-confirm"); t = await TradeCommand(b, g, t, "final-confirm");
        Assert.Equal("completed", t.State);
        Assert.Equal(note.Id, (await a.GetFromJsonAsync<HoldingPage>($"/api/v1/groups/{g.Id}/inventory", TestContext.Current.CancellationToken))!.Items.Single().Id);
        Assert.Equal(book.Id, (await b.GetFromJsonAsync<HoldingPage>($"/api/v1/groups/{g.Id}/inventory", TestContext.Current.CancellationToken))!.Items.Single().Id);
        var received = (await a.GetFromJsonAsync<Letter>($"/api/v1/groups/{g.Id}/letters/{note.LetterId}", TestContext.Current.CancellationToken))!; Assert.True(received.Locked); Assert.Equal(bb.Character.Id, received.Author.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/v1/groups/{g.Id}/inventory/{book.Id}", TestContext.Current.CancellationToken)).StatusCode);
    }
    [Fact]
    public async Task Canonical_holding_reads_and_reductions_distinguish_self_from_administration_and_replay_once()
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); await Login(a, "417"); var g = await Create(a); using var b = f.CreateClient(); var bb = await Login(b, "418"); await Join(b, await Invite(a, g));
        var recipe = await Recipe(a, g); await GrantPotions(a, g, bb.Character.Id);
        var h = (await Command<ProductionResult>(b, HttpMethod.Post, $"/api/v1/groups/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 3))).Holding;
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync($"/api/v1/groups/{g.Id}/members/{bb.Character.Id}/holdings", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync($"/api/v1/groups/{g.Id}/members/{bb.Character.Id}/balances", TestContext.Current.CancellationToken)).StatusCode);
        var path = $"/api/v1/groups/{g.Id}/members/{bb.Character.Id}/holdings/{h.Id}";
        h = await Command<Holding>(b, HttpMethod.Patch, path, new HoldingChange(h.Version, 2)); Assert.Equal(2, h.Quantity);
        using var missingReason = Request(HttpMethod.Patch, path, new HoldingChange(h.Version, 1)); Assert.Equal(HttpStatusCode.UnprocessableEntity, (await a.SendAsync(missingReason, TestContext.Current.CancellationToken)).StatusCode);
        using var increase = Request(HttpMethod.Patch, path, new HoldingChange(h.Version, 4)); Assert.Equal(HttpStatusCode.UnprocessableEntity, (await b.SendAsync(increase, TestContext.Current.CancellationToken)).StatusCode);
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var body = new HoldingDeletion(h.Version, "Remove remaining items");
        using var deletion = Request(HttpMethod.Delete, path, body, key); using var first = await a.SendAsync(deletion, TestContext.Current.CancellationToken); Assert.True(first.IsSuccessStatusCode);
        using var retry = Request(HttpMethod.Delete, path, body, key); using var replay = await a.SendAsync(retry, TestContext.Current.CancellationToken); Assert.True(replay.IsSuccessStatusCode); Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(9, (await b.GetFromJsonAsync<Quotas>($"/api/v1/groups/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "potion_points").Used);
    }
}
