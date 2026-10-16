using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private Task<Trade> TradeCommand(HttpClient c, Group g, Trade t, string action) => Command<Trade>(c, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/{action}", new TradeRevision(t.Revision));
    private Task<Trade> Offer(HttpClient c, Group g, Trade t, string coins, params TradeLine[] lines) => Command<Trade>(c, HttpMethod.Put, $"/api/v1/groups/{g.Id}/trades/{t.Id}/offers/mine", new TradeOfferWrite(t.Revision, g.Currency.Id, coins, lines));
    private async Task<Trade> ReadTrade(HttpClient c, Group g, Trade t) => (await c.GetFromJsonAsync<Trade>($"/api/v1/groups/{g.Id}/trades/{t.Id}", TestContext.Current.CancellationToken))!;
    private async Task<Holding> CurrentHolding(HttpClient c, Group g, Guid id) => (await c.GetFromJsonAsync<HoldingDetails>($"/api/v1/groups/{g.Id}/inventory/{id}", TestContext.Current.CancellationToken))!.Holding;
    [Fact]
    public async Task Trade_reserves_assets_seals_bodies_and_requires_final_approval_for_same_key_exchange()
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); var ta = await Login(a, "400"); var g = await Create(a);
        using var b = f.CreateClient(); var tb = await Login(b, "401"); await Join(b, await Invite(a, g));
        using var outsider = f.CreateClient(); await Login(outsider, "402"); await Join(outsider, await Invite(a, g));
        var recipe = await Recipe(a, g); var potion = (await Command<ProductionResult>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 3))).Holding;
        var books = (await a.GetFromJsonAsync<CategoryPage>($"/api/v1/groups/{g.Id}/categories", TestContext.Current.CancellationToken))!.Items.Single(x => x.Name == "Books");
        var letter = (await Command<ProductionResult>(b, HttpMethod.Post, $"/api/v1/groups/{g.Id}/inventory/letters", new LetterCreate(books.Id, "Sealed note", "PRIVATE BODY", "note"))).Holding;
        await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(ta.Character.Id, "100", "Seed"));
        var t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(tb.Character.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/groups/{g.Id}/trades/{t.Id}", TestContext.Current.CancellationToken)).StatusCode);
        t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "30", new TradeLine(potion.Id, 2));
        var reserved = await CurrentHolding(a, g, potion.Id); Assert.Equal(2, reserved.Reserved); Assert.Equal(1, reserved.Available);
        using var discard = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/inventory/{potion.Id}/discard", new DiscardRequest(2, reserved.Version));
        Assert.Equal(HttpStatusCode.Conflict, (await a.SendAsync(discard, TestContext.Current.CancellationToken)).StatusCode);
        t = await Offer(b, g, t, "0", new TradeLine(letter.Id, 1)); Assert.False(t.Initiator.Confirmed); Assert.False(t.Recipient.Confirmed);
        Assert.DoesNotContain("PRIVATE BODY", await a.GetStringAsync($"/api/v1/groups/{g.Id}/trades/{t.Id}", TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/v1/groups/{g.Id}/letters/{letter.LetterId}", TestContext.Current.CancellationToken)).StatusCode);
        using var edit = Request(HttpMethod.Patch, $"/api/v1/groups/{g.Id}/letters/{letter.LetterId}", new LetterEdit("Changed", ""), etag: $"\"letter:{letter.LetterId:N}:1\"");
        Assert.Equal(HttpStatusCode.Forbidden, (await b.SendAsync(edit, TestContext.Current.CancellationToken)).StatusCode);
        var staleRevision = t.Revision - 1;
        using var stale = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/confirm", new TradeRevision(staleRevision));
        Assert.Equal(HttpStatusCode.Conflict, (await a.SendAsync(stale, TestContext.Current.CancellationToken)).StatusCode);
        t = await TradeCommand(a, g, t, "confirm"); t = await TradeCommand(b, g, t, "confirm");
        Assert.Equal("confirming", t.State);
        Assert.Equal("100", (await a.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!.MyBalance.Owned);
        t = await TradeCommand(a, g, t, "final-confirm");
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var path = $"/api/v1/groups/{g.Id}/trades/{t.Id}/final-confirm"; var body = new TradeRevision(t.Revision);
        using var confirm = Request(HttpMethod.Post, path, body, key); using var complete = await b.SendAsync(confirm, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        using var retry = Request(HttpMethod.Post, path, body, key); using var replay = await b.SendAsync(retry, TestContext.Current.CancellationToken); Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        var done = (await complete.Content.ReadFromJsonAsync<CommandResult<Trade>>(TestContext.Current.CancellationToken))!.Result;
        Assert.Equal("completed", done.State); Assert.NotNull(done.CompletionOperationId);
        var ga = (await a.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!;
        var gb = (await b.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!;
        Assert.Equal("70", ga.MyBalance.Owned); Assert.Equal("30", gb.MyBalance.Owned); Assert.Equal("0", ga.MyBalance.Reserved);
        Assert.Equal(1, (await CurrentHolding(a, g, potion.Id)).Quantity);
        var incoming = (await b.GetFromJsonAsync<HoldingPage>($"/api/v1/groups/{g.Id}/inventory", TestContext.Current.CancellationToken))!.Items.Single(); Assert.Equal(2, incoming.Quantity);
        var written = (await a.GetFromJsonAsync<Letter>($"/api/v1/groups/{g.Id}/letters/{letter.LetterId}", TestContext.Current.CancellationToken))!; Assert.Equal("PRIVATE BODY", written.Body); Assert.True(written.Locked); Assert.Equal("note", written.TypeId);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(3, await db.Holdings.Where(x => x.TypeId == "potion").SumAsync(x => x.Quantity, TestContext.Current.CancellationToken)); Assert.Empty(await db.TradeSlots.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Operations.Where(x => x.Kind == "trade.complete").ToArrayAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Trade_restart_expiry_and_restriction_release_reservations_without_quota_refund()
    {
        AmorRP.Contracts.Auth.SessionTokens aa, bb; Group g; Trade t; Guid potionId;
        using (var f = Factory())
        {
            await Migrate(f); using var a = f.CreateClient(); aa = await Login(a, "403"); g = await Create(a); using var b = f.CreateClient(); bb = await Login(b, "404"); await Join(b, await Invite(a, g));
            var recipe = await Recipe(a, g); potionId = (await Command<ProductionResult>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, 1, 2))).Holding.Id;
            t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "0", new TradeLine(potionId, 2));
        }
        using var restarted = Factory(); using var ac = restarted.CreateClient(); using var bc = restarted.CreateClient(); ac.DefaultRequestHeaders.Authorization = new("Bearer", aa.AccessToken); bc.DefaultRequestHeaders.Authorization = new("Bearer", bb.AccessToken);
        Assert.Equal("open", (await ReadTrade(ac, g, t)).State); Assert.Equal(2, (await CurrentHolding(ac, g, potionId)).Reserved);
        clock.UtcNow += TimeSpan.FromMinutes(11); Assert.Equal("expired", (await ReadTrade(bc, g, t)).State); Assert.Equal(0, (await CurrentHolding(ac, g, potionId)).Reserved);
        t = await Command<Trade>(ac, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(bc, g, t, "accept"); t = await Offer(ac, g, t, "0", new TradeLine(potionId, 1));
        var member = await Member(ac, g, bb.Character.Id); await Command<Member>(ac, HttpMethod.Put, $"/api/v1/groups/{g.Id}/members/{bb.Character.Id}/trade-restriction", new MemberRestriction(true, "Restricted"), member.Etag);
        Assert.Equal("cancelled", (await ReadTrade(bc, g, t)).State); Assert.Equal(0, (await CurrentHolding(ac, g, potionId)).Reserved);
        Assert.Equal(6, (await ac.GetFromJsonAsync<Quotas>($"/api/v1/groups/{g.Id}/quotas", TestContext.Current.CancellationToken))!.Items.Single(x => x.Kind == "potion_points").Used);
    }
    [Fact]
    public async Task Concurrent_confirm_and_removal_have_one_consistent_outcome_and_removal_differs_from_block()
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); var aa = await Login(a, "405"); var g = await Create(a); using var b = f.CreateClient(); var bb = await Login(b, "406"); await Join(b, await Invite(a, g));
        await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(aa.Character.Id, "50", "Seed"));
        var t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "20"); t = await TradeCommand(a, g, t, "confirm");
        t = await TradeCommand(b, g, t, "confirm"); t = await TradeCommand(a, g, t, "final-confirm");
        var member = await Member(a, g, bb.Character.Id);
        using var removal = Request(HttpMethod.Delete, $"/api/v1/groups/{g.Id}/members/{bb.Character.Id}", new MemberRemoval("Removed"), etag: member.Etag);
        using var confirm = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/final-confirm", new TradeRevision(t.Revision));
        var outcomes = await Task.WhenAll(a.SendAsync(removal, TestContext.Current.CancellationToken), b.SendAsync(confirm, TestContext.Current.CancellationToken)); Assert.True(outcomes[0].IsSuccessStatusCode); foreach (var o in outcomes) o.Dispose();
        var ended = await ReadTrade(b, g, t); Assert.Contains(ended.State, new[] { "completed", "cancelled" });
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var balances = await db.Memberships.Where(x => x.GroupId == g.Id).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(50, balances.Sum(x => x.Owned)); Assert.All(balances, x => Assert.Equal(0, x.Reserved));
            Assert.Equal(ended.State == "completed" ? 20 : 0, balances.Single(x => x.CharacterId == bb.Character.Id).Owned);
        }
        Assert.Equal("dormant", (await Member(a, g, bb.Character.Id)).Status); await Join(b, await Invite(a, g));
        member = await Member(a, g, bb.Character.Id); await Command<Member>(a, HttpMethod.Put, $"/api/v1/groups/{g.Id}/members/{bb.Character.Id}/block", new MemberRemoval("Blocked"), member.Etag);
        using var denied = Request(HttpMethod.Post, "/api/v1/groups/join", new JoinGroup((await Invite(a, g)).InvitationCode, true)); Assert.Equal(HttpStatusCode.Forbidden, (await b.SendAsync(denied, TestContext.Current.CancellationToken)).StatusCode);
        member = await Member(a, g, bb.Character.Id); var unblocked = await Command<Member>(a, HttpMethod.Delete, $"/api/v1/groups/{g.Id}/members/{bb.Character.Id}/block", new MemberRemoval("Unblocked"), member.Etag); Assert.Equal("dormant", unblocked.Status);
        await Join(b, await Invite(a, g)); var self = (await b.GetFromJsonAsync<Member>($"/api/v1/groups/{g.Id}/members/{bb.Character.Id}", TestContext.Current.CancellationToken))!;
        await Command<Member>(b, HttpMethod.Delete, $"/api/v1/groups/{g.Id}/members/{bb.Character.Id}", new MemberRemoval(""), self.Etag); Assert.Equal("dormant", (await Member(a, g, bb.Character.Id)).Status);
    }
}
