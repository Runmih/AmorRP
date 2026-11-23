using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Permits;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Public_permit_buys_after_one_approval_and_settles_debt_once()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        var ownerId = (await Login(owner, "21300")).Character.Id;
        var season = await Create(owner);
        var buyerGroup = await TreasuryGroup(owner, season, "Purchasing group");
        var creditor = await TreasuryGroup(owner, season, "Creditor group");
        var path = $"/api/v2/seasons/{season.Id}/permits";
        await Fund(owner, season, ownerId, "70");
        await TreasuryMove(owner, season, buyerGroup, "70");
        await Command<PermitPolicy>(owner, HttpMethod.Put, path + "/policy", new PermitPolicy("20", true));
        await DebtPolicy(owner, season, true);
        var effect = new MoodleEffect("Registered", "Permit is active.", 200001, 30, true);
        await Command<OwnershipEffectPolicy>(owner, HttpMethod.Put, $"/api/v2/seasons/{season.Id}/ownership-effect",
            new OwnershipEffectPolicy(1, effect));

        using var subject = factory.CreateClient();
        var subjectId = (await Login(subject, "21301")).Character.Id;
        await Enroll(subject, season);
        await TrustBilling(subject, season, creditor);
        await Charge(owner, season, creditor, subjectId, "15");
        var pending = await Command<PermitListing>(owner, HttpMethod.Post, path + "/listings", new PermitListingEdit(subjectId));
        Assert.Empty((await owner.GetFromJsonAsync<PermitShop>(path, ct))!.Listings);
        var purchasePath = path + "/purchases";
        var purchase = new PermitPurchase(subjectId, buyerGroup.Id, "35");
        using (var req = Request(HttpMethod.Post, purchasePath, purchase, etag: pending.Etag))
        using (var denied = await owner.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);

        var listing = await Command<PermitListing>(subject, HttpMethod.Put, path + "/listings/mine",
            new PermitListingConsent(true, pending.Total), pending.Etag);
        Assert.Equal("35", listing.Total);
        Assert.Equal(listing, Assert.Single((await owner.GetFromJsonAsync<PermitShop>(path, ct))!.Listings));

        using (var req = Request(HttpMethod.Post, purchasePath, purchase, etag: pending.Etag))
        using (var denied = await owner.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.PreconditionFailed, denied.StatusCode);
        using (var req = Request(HttpMethod.Post, purchasePath, purchase with { ExpectedTotal = "20" }, etag: listing.Etag))
        using (var denied = await owner.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using (var req = Request(HttpMethod.Post, path + "/offers", new PermitOfferCreate(subjectId, buyerGroup.Id, false)))
        using (var denied = await owner.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);

        var key = Guid.CreateVersion7(clock.UtcNow);
        using var buyRequest = Request(HttpMethod.Post, purchasePath, purchase, key, listing.Etag);
        using var competingRequest = Request(HttpMethod.Post, purchasePath, purchase, etag: listing.Etag);
        var results = await Task.WhenAll(owner.SendAsync(buyRequest, ct), owner.SendAsync(competingRequest, ct));
        using var bought = Assert.Single(results, x => x.IsSuccessStatusCode);
        using var lost = Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        var permit = (await bought.Content.ReadFromJsonAsync<CommandResult<OwnershipPermit>>(ct))!.Result;
        var winningKey = results[0].IsSuccessStatusCode ? key : Guid.Parse(competingRequest.Headers.GetValues("Idempotency-Key").Single());
        using (var req = Request(HttpMethod.Post, purchasePath, purchase, winningKey, listing.Etag))
        using (var replay = await owner.SendAsync(req, ct))
        {
            Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
            Assert.Equal(permit, (await replay.Content.ReadFromJsonAsync<CommandResult<OwnershipPermit>>(ct))!.Result);
        }
        var shop = (await owner.GetFromJsonAsync<PermitShop>(path, ct))!;
        Assert.Empty(shop.Listings);
        Assert.Empty(shop.Offers);
        Assert.Equal(permit.Id, Assert.Single(shop.Permits).Id);
        Assert.Equal(subjectId, permit.CharacterId);
        const string effectsPath = "/api/v2/me/moodle-effects";
        var granted = Assert.Single((await subject.GetFromJsonAsync<MoodleGrants>(effectsPath, ct))!.Active);
        Assert.Equal(effect, granted.Effect);
        Assert.Null(granted.ExpiresAt);
        Assert.Empty((await owner.GetFromJsonAsync<MoodleGrants>(effectsPath, ct))!.Active);
        var groupPath = TreasuryPath(season, buyerGroup)[..^9];
        var group = (await subject.GetFromJsonAsync<GroupProfile>(groupPath, ct))!;
        Assert.True(group.BillingTrustLocked && group.IsTrusted);
        using (var req = Request(HttpMethod.Put, groupPath + "/trust", new GroupTrustEdit(false),
                   etag: $"\"group-trust:{buyerGroup.Id:N}:{subjectId:N}:{group.TrustVersion}\""))
        using (var denied = await subject.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);

        Assert.Equal("35", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(season, buyerGroup), ct))!.Owned);
        Assert.Equal("15", (await owner.GetFromJsonAsync<TreasuryBalance>(TreasuryPath(season, creditor), ct))!.Owned);
        Assert.Equal("0", (await subject.GetFromJsonAsync<BillPage>($"/api/v2/seasons/{season.Id}/bills/mine", ct))!.TotalOutstanding);
        await Command<OwnershipPermit>(owner, HttpMethod.Delete, path + "/" + permit.Id);
        Assert.False((await subject.GetFromJsonAsync<GroupProfile>(groupPath, ct))!.BillingTrustLocked);
        var after = (await subject.GetFromJsonAsync<MoodleGrants>(effectsPath, ct))!;
        Assert.Empty(after.Active);
        Assert.Equal(granted.Id, Assert.Single(after.Remove));
    }
    [Fact]
    public async Task Private_permit_quote_is_participant_only_and_changed_assets_require_new_consent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var buyer = factory.CreateClient();
        var buyerId = (await Login(buyer, "21310")).Character.Id;
        var season = await Create(buyer);
        var group = await TreasuryGroup(buyer, season);
        var path = $"/api/v2/seasons/{season.Id}/permits";
        await Fund(buyer, season, buyerId, "60");
        await TreasuryMove(buyer, season, group, "60");
        await Command<PermitPolicy>(buyer, HttpMethod.Put, path + "/policy", new PermitPolicy("20", true));
        using var subject = factory.CreateClient();
        var subjectId = (await Login(subject, "21311")).Character.Id;
        await Enroll(subject, season);
        await Fund(buyer, season, subjectId, "8");
        var offer = await Command<PermitOffer>(buyer, HttpMethod.Post, path + "/offers", new PermitOfferCreate(subjectId, group.Id, true));
        var pathOffer = path + $"/offers/{offer.Id}";
        Assert.Equal("0", offer.AssetsCurrency);
        var quote = Assert.Single((await subject.GetFromJsonAsync<PermitShop>(path, ct))!.Offers);
        Assert.Equal("8", quote.AssetsCurrency);
        await Command<PermitOffer>(subject, HttpMethod.Put, pathOffer + "/consent", new PermitQuoteAction(quote.Quote));
        using var stranger = factory.CreateClient();
        await Login(stranger, "21312");
        await Enroll(stranger, season);
        var publicView = (await stranger.GetFromJsonAsync<PermitShop>(path, ct))!;
        Assert.Empty(publicView.Listings);
        Assert.Empty(publicView.Offers);
        await Fund(buyer, season, subjectId, "2");
        using (var req = Request(HttpMethod.Post, pathOffer + "/register", new PermitQuoteAction(quote.Quote)))
        using (var stale = await buyer.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("10", (await OwnBalance(subject, season)).Owned);
        var fresh = Assert.Single((await subject.GetFromJsonAsync<PermitShop>(path, ct))!.Offers);
        Assert.False(fresh.Accepted);
        await Command<PermitOffer>(subject, HttpMethod.Put, pathOffer + "/consent", new PermitQuoteAction(fresh.Quote));
        await Command<OwnershipPermit>(buyer, HttpMethod.Post, pathOffer + "/register", new PermitQuoteAction(fresh.Quote));
        Assert.Equal("10", (await OwnBalance(buyer, season)).Owned);
        Assert.Equal("0", (await OwnBalance(subject, season)).Owned);
    }
}
