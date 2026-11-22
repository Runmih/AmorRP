using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Permits;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Permit_listing_requires_consent_and_registration_pays_debt_then_locks_trust_until_release()
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

        using var subject = factory.CreateClient();
        var subjectId = (await Login(subject, "21301")).Character.Id;
        await Enroll(subject, season);
        await TrustBilling(subject, season, creditor);
        await Charge(owner, season, creditor, subjectId, "15");
        var pending = await Command<PermitListing>(owner, HttpMethod.Post, path + "/listings", new PermitListingEdit(subjectId));
        Assert.Empty((await owner.GetFromJsonAsync<PermitShop>(path, ct))!.Listings);
        var listing = await Command<PermitListing>(subject, HttpMethod.Put, path + "/listings/mine",
            new PermitListingConsent(true, pending.Total), pending.Etag);
        Assert.Equal("35", listing.Total);

        var offer = await Command<PermitOffer>(owner, HttpMethod.Post, path + "/offers", new PermitOfferCreate(subjectId, buyerGroup.Id, false));
        var registerPath = path + $"/offers/{offer.Id}/register";
        using (var req = Request(HttpMethod.Post, registerPath, new PermitQuoteAction(offer.Quote)))
        using (var denied = await owner.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        await Command<PermitOffer>(subject, HttpMethod.Put, path + $"/offers/{offer.Id}/consent", new PermitQuoteAction(offer.Quote));
        var permit = await Command<OwnershipPermit>(owner, HttpMethod.Post, registerPath, new PermitQuoteAction(offer.Quote));
        Assert.Equal(subjectId, permit.CharacterId);
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
    }
}
