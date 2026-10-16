using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Leading_zero_currency_is_normalized_and_edits_after_first_approval_cancel_even_with_stale_revision()
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); var aa = await Login(a, "430"); var g = await Create(a); using var b = f.CreateClient(); var bb = await Login(b, "431"); await Join(b, await Invite(a, g));
        await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(aa.Character.Id, "10", "Seed"));
        var t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept");
        t = await Offer(a, g, t, "04"); Assert.Equal("4", t.Initiator.CurrencyAmount);
        t = await TradeCommand(a, g, t, "confirm");
        var cancelled = await Command<Trade>(b, HttpMethod.Put, $"/api/v1/groups/{g.Id}/trades/{t.Id}/offers/mine", new TradeOfferWrite(t.Revision - 1, g.Currency.Id, "0", []));
        Assert.Equal("cancelled", cancelled.State); Assert.Equal("0", (await a.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!.MyBalance.Reserved);
        Assert.Equal("10", (await a.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!.MyBalance.Owned);
    }
    [Fact]
    public async Task Final_confirmation_cannot_skip_first_approvals_and_restart_retains_exact_final_review()
    {
        AmorRP.Contracts.Auth.SessionTokens aa, bb; Group g; Trade t;
        using (var f = Factory())
        {
            await Migrate(f); using var a = f.CreateClient(); aa = await Login(a, "432"); g = await Create(a); using var b = f.CreateClient(); bb = await Login(b, "433"); await Join(b, await Invite(a, g));
            await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(aa.Character.Id, "10", "Seed"));
            t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "4");
            using var bypass = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/final-confirm", new TradeRevision(t.Revision)); Assert.Equal(HttpStatusCode.Conflict, (await a.SendAsync(bypass, TestContext.Current.CancellationToken)).StatusCode);
            t = await TradeCommand(a, g, t, "confirm"); t = await TradeCommand(b, g, t, "confirm"); t = await TradeCommand(a, g, t, "final-confirm");
            Assert.Equal("confirming", t.State);
        }
        using var restarted = Factory(); using var ac = restarted.CreateClient(); using var bc = restarted.CreateClient(); ac.DefaultRequestHeaders.Authorization = new("Bearer", aa.AccessToken); bc.DefaultRequestHeaders.Authorization = new("Bearer", bb.AccessToken);
        t = await ReadTrade(bc, g, t); Assert.True(t.Initiator.FinalConfirmed); Assert.False(t.Recipient.FinalConfirmed);
        using var stale = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/final-confirm", new TradeRevision(t.Revision - 1)); Assert.Equal(HttpStatusCode.Conflict, (await bc.SendAsync(stale, TestContext.Current.CancellationToken)).StatusCode);
        t = await TradeCommand(bc, g, t, "final-confirm"); Assert.Equal("completed", t.State);
        Assert.Equal("6", (await ac.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!.MyBalance.Owned);
    }
    [Fact]
    public async Task Simultaneous_edit_and_last_final_confirmation_never_exchange_an_edited_offer()
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); var aa = await Login(a, "436"); var g = await Create(a); using var b = f.CreateClient(); var bb = await Login(b, "437"); await Join(b, await Invite(a, g));
        await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(aa.Character.Id, "10", "Seed"));
        var t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "4");
        t = await TradeCommand(a, g, t, "confirm"); t = await TradeCommand(b, g, t, "confirm"); t = await TradeCommand(a, g, t, "final-confirm");
        using var edit = Request(HttpMethod.Put, $"/api/v1/groups/{g.Id}/trades/{t.Id}/offers/mine", new TradeOfferWrite(t.Revision, g.Currency.Id, "9", []));
        using var final = Request(HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/final-confirm", new TradeRevision(t.Revision));
        var outcomes = await Task.WhenAll(a.SendAsync(edit, TestContext.Current.CancellationToken), b.SendAsync(final, TestContext.Current.CancellationToken));
        Assert.Single(outcomes, x => x.IsSuccessStatusCode); Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.Conflict); foreach (var response in outcomes) response.Dispose();
        t = await ReadTrade(a, g, t); Assert.Contains(t.State, new[] { "completed", "cancelled" });
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var balances = await db.Memberships.ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, balances.Sum(x => x.Owned)); Assert.All(balances, x => Assert.Equal(0, x.Reserved));
        Assert.Equal(t.State == "completed" ? 4 : 0, balances.Single(x => x.CharacterId == bb.Character.Id).Owned);
        Assert.Empty(await db.TradeSlots.ToArrayAsync(TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("cancel")]
    [InlineData("expiry")]
    [InlineData("restriction")]
    public async Task Final_review_releases_reservations_on_cancellation_expiry_or_restriction(string action)
    {
        using var f = Factory(); await Migrate(f); using var a = f.CreateClient(); var aa = await Login(a, "434"); var g = await Create(a); using var b = f.CreateClient(); var bb = await Login(b, "435"); await Join(b, await Invite(a, g));
        await Command<Balance>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/currency/adjustments", new CurrencyAdjustment(aa.Character.Id, "10", "Seed"));
        var t = await Command<Trade>(a, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades", new TradeCreate(bb.Character.Id)); t = await TradeCommand(b, g, t, "accept"); t = await Offer(a, g, t, "4");
        t = await TradeCommand(a, g, t, "confirm"); t = await TradeCommand(b, g, t, "confirm");
        if (action == "cancel") t = await Command<Trade>(b, HttpMethod.Post, $"/api/v1/groups/{g.Id}/trades/{t.Id}/cancel", new TradeRevision(1));
        else if (action == "expiry") clock.UtcNow += TimeSpan.FromMinutes(11);
        else { var m = await Member(a, g, bb.Character.Id); await Command<Member>(a, HttpMethod.Put, $"/api/v1/groups/{g.Id}/members/{bb.Character.Id}/trade-restriction", new MemberRestriction(true, "Restriction"), m.Etag); }
        t = await ReadTrade(b, g, t); Assert.Equal(action == "expiry" ? "expired" : "cancelled", t.State);
        var updated = (await a.GetFromJsonAsync<Group>($"/api/v1/groups/{g.Id}", TestContext.Current.CancellationToken))!; Assert.Equal("0", updated.MyBalance.Reserved); Assert.Equal("10", updated.MyBalance.Owned);
        await using var scope = f.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); Assert.Empty(await db.TradeSlots.ToArrayAsync(TestContext.Current.CancellationToken));
    }
}
