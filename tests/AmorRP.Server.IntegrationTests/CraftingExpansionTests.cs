using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace AmorRP.Server.IntegrationTests;
public sealed partial class FoundationTests
{
    [Theory]
    [InlineData("potion", false)] [InlineData("poison", false)] [InlineData("explosive", false)] [InlineData("drug", false)] [InlineData("medicine", false)]
    [InlineData("meal", true)] [InlineData("drink", true)] [InlineData("dessert", true)]
    public async Task Recipe_items_preserve_type_spend_separate_budget_trade_and_use(string type, bool cooking)
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1020"); var c = await Command<Community>(owner, HttpMethod.Post, "/api/v2/communities", new CommunityCreate("Crafts", "", "Coins", "C", 12, 5, 12));
        using var member = f.CreateClient(); var actor = (await Login(member, "1021")).Character.Id; await Join(member, await Invite(owner, c));
        var prefix = $"/api/v2/communities/{c.Id}/";
        var categories = (await owner.GetFromJsonAsync<CategoryPage>(prefix + "categories", ct))!.Items;
        var category = categories.Single(x => x.AllowedTypeIds.Contains(type));
        var recipe = await Command<Definition>(owner, HttpMethod.Post, prefix + "item-definitions", new DefinitionWrite(type, "Test " + type, category.Id, "RP item", true, new(3, "uses a test item.")));
        using (var denied = Request(HttpMethod.Post, prefix + "inventory/potions", new PotionCreate(recipe.Id, 1, 2)))
        using (var response = await member.SendAsync(denied, ct)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await GrantHome(owner, c, actor, cooking ? "items.cooking.create" : "items.potion.create");
        var produced = await Command<ProductionResult>(member, HttpMethod.Post, prefix + "inventory/potions", new PotionCreate(recipe.Id, 1, 2));
        Assert.Equal(type, produced.Holding.TypeId); Assert.Equal(6, produced.Quota.Remaining); Assert.Equal(cooking ? "cooking_points" : "potion_points", produced.Quota.Kind);
        var quotas = (await member.GetFromJsonAsync<Quotas>(prefix + "quotas", ct))!.Items;
        Assert.Equal(12, quotas.Single(x => x.Kind == (cooking ? "potion_points" : "cooking_points")).Remaining);
        var trade = await Command<Trade>(member, HttpMethod.Post, prefix + "trades", new TradeCreate(c.OwnerCharacterId));
        trade = await TradeCommand(owner, c, trade, "accept"); trade = await Offer(member, c, trade, "0", new TradeLine(produced.Holding.Id, 1));
        trade = await TradeCommand(owner, c, trade, "confirm"); trade = await TradeCommand(member, c, trade, "confirm");
        trade = await TradeCommand(owner, c, trade, "final-confirm"); trade = await TradeCommand(member, c, trade, "final-confirm"); Assert.Equal("completed", trade.State);
        var received = Assert.Single((await owner.GetFromJsonAsync<HoldingPage>(prefix + "inventory?typeId=" + type, ct))!.Items);
        Assert.Equal(type, received.TypeId);
        var used = await Command<ConsumptionResult>(owner, HttpMethod.Post, prefix + "inventory/" + received.Id + "/use", new ConsumptionRequest(received.Version, true, "say"));
        Assert.Equal("uses a test item.", used.Message);
        using var recipeRequest = Request(HttpMethod.Post, prefix + "item-definitions", new DefinitionWrite(type, "Denied", category.Id, "", true, new(1, "test")));
        using var recipeDenied = await member.SendAsync(recipeRequest, ct); Assert.Equal(HttpStatusCode.Forbidden, recipeDenied.StatusCode);
    }
}
