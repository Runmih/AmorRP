using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Shop;
using AmorRP.Contracts.Currency;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Shop_reserves_final_stock_and_replaying_a_purchase_does_not_charge_twice()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21280");
        var season = await Create(owner);
        var definition = await Recipe(owner, season);
        var path = $"/api/v2/seasons/{season.Id}/shop";
        var product = await Command<ShopProduct>(owner, HttpMethod.Post, path + "/products",
            new ShopProductWrite(definition.Id, "20", "character", 1, 1, true));
        using var buyer = factory.CreateClient();
        var buyerId = (await Login(buyer, "21281")).Character.Id;
        await Enroll(buyer, season);
        await Fund(owner, season, buyerId, "50");
        var buy = new ShopBuy(product.Id, product.Version, 1);
        var key = Guid.CreateVersion7(clock.GetUtcNow());
        for (var i = 0; i < 2; i++)
        {
            using var request = Request(HttpMethod.Post, path + "/purchases", buy, key);
            using var response = await buyer.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var stale = Request(HttpMethod.Post, path + "/purchases", buy);
        using var conflict = await buyer.SendAsync(stale, ct);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("30", (await OwnBalance(buyer, season)).Owned);
        Assert.Equal(0, Assert.Single((await buyer.GetFromJsonAsync<ShopProducts>(path + "/products", ct))!.Products).Stock);
        var sold = Assert.Single((await owner.GetFromJsonAsync<ShopProducts>(path + "/products", ct))!.Products);
        await Command<Guid>(owner, HttpMethod.Delete, path + "/products/" + sold.Id, etag: sold.Etag);
        Assert.Empty((await owner.GetFromJsonAsync<ShopProducts>(path + "/products", ct))!.Products);
        using (var request = Request(HttpMethod.Post, path + "/purchases", buy, key))
        using (var replay = await buyer.SendAsync(request, ct)) Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal("20", (await owner.GetFromJsonAsync<BillingPolicy>($"/api/v2/seasons/{season.Id}/billing-policy", ct))!.SeasonTreasury);
    }
}
