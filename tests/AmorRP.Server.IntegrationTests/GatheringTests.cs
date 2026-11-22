using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Supplies_allow_advanced_crafting_above_the_base_and_expire_at_the_shared_reset()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        var ownerId = (await Login(owner, "21321")).Character.Id;
        var season = await Create(owner);
        var prefix = $"/api/v2/seasons/{season.Id}";
        var recipe = await Recipe(owner, season, cost: 25);
        var design = await Command<GatheringSettings>(owner, HttpMethod.Put, prefix + "/gathering",
            new GatheringSettingsEdit(0, 8, 2, 20, 3, 10));
        var pack = Assert.Single(design.Packages, x => x.TypeId == CraftingTypes.AlchemySupplies);
        var gather = new GatherSupplies(pack.TypeId, pack.Revision, 2);

        using var other = factory.CreateClient();
        await Login(other, "21322");
        await Enroll(other, season);
        using (var req = Request(HttpMethod.Post, prefix + "/gathering/packages", gather))
        using (var denied = await other.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain((await other.GetFromJsonAsync<Quotas>(prefix + "/quotas", ct))!.Items, x => x.Kind == "gathering_points");

        var created = await Command<ProductionResult>(owner, HttpMethod.Post, prefix + "/gathering/packages", gather);
        Assert.Equal(4, created.Quota.Remaining);
        var use = new UseSupplies(created.Holding.Version);
        var usePath = prefix + $"/inventory/{created.Holding.Id}/use-supplies";
        var useKey = Guid.CreateVersion7(clock.GetUtcNow());
        var consumed = await Command<ProductionResult>(owner, HttpMethod.Post, usePath, use);
        Assert.Equal(32, consumed.Quota.Remaining);
        Assert.Equal(20, consumed.Quota.Bonus);
        Assert.Equal(1, consumed.Holding.Quantity);
        using (var stale = Request(HttpMethod.Post, usePath, use, useKey))
        using (var response = await owner.SendAsync(stale, ct))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var crafted = await Command<ProductionResult>(owner, HttpMethod.Post, prefix + "/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, 1));
        Assert.Equal(7, crafted.Quota.Remaining);

        clock.UtcNow = design.ResetAt.AddMinutes(1);
        Assert.Equal(ownerId, (await Login(owner, "21321", known: ownerId)).Character.Id);
        var reset = (await owner.GetFromJsonAsync<Quotas>(prefix + "/quotas", ct))!;
        Assert.Equal(12, Assert.Single(reset.Items, x => x.Kind == "potion_points").Remaining);
        Assert.Equal(0, Assert.Single(reset.Items, x => x.Kind == "potion_points").Bonus);
        Assert.Equal(8, Assert.Single(reset.Items, x => x.Kind == "gathering_points").Remaining);
    }
}
