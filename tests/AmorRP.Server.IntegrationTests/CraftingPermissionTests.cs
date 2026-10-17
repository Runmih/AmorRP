using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Inventory;
using Xunit;
namespace AmorRP.Server.IntegrationTests;
public sealed partial class FoundationTests
{
    [Theory]
    [InlineData("items.potion.define", "poison", true)]
    [InlineData("items.potion.define", "meal", false)]
    [InlineData("items.cooking.define", "meal", true)]
    [InlineData("items.cooking.define", "poison", false)]
    public async Task Alchemy_and_Cooking_recipe_permissions_are_independent(string permission, string type, bool allowed)
    {
        var ct = TestContext.Current.CancellationToken;
        using var f = Factory(); await Migrate(f); using var owner = f.CreateClient(); await Login(owner, "1050"); var c = await Create(owner);
        using var member = f.CreateClient(); var actor = (await Login(member, "1051")).Character.Id; await Join(member, await Invite(owner, c));
        await GrantHome(owner, c, actor, permission);
        var prefix = $"/api/v2/communities/{c.Id}/";
        var categories = (await member.GetFromJsonAsync<CategoryPage>(prefix + "categories", ct))!.Items;
        using var request = Request(HttpMethod.Post, prefix + "item-definitions", new DefinitionWrite(type, "Recipe", categories.Single(x => x.AllowedTypeIds.Contains(type)).Id, "Description", true, new(1, "uses an item.")));
        using var response = await member.SendAsync(request, ct);
        Assert.Equal(allowed ? HttpStatusCode.Created : HttpStatusCode.Forbidden, response.StatusCode);
    }
}
