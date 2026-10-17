using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Literal_heart_message_survives_recipe_production_and_consumption_and_placeholder_errors_explain_rejection()
    {
        using var f = Factory(); await Migrate(f); using var c = f.CreateClient(); await Login(c, "500"); var g = await Create(c);
        const string message = "drinks a potion <3";
        var recipe = await Recipe(c, g, message: message);
        var produced = await Command<ProductionResult>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, 2));
        var details = await c.GetFromJsonAsync<HoldingDetails>($"/api/v2/communities/{g.Id}/inventory/{produced.Holding.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(message, details!.Definition!.Potion.UseMessage);
        var used = await Command<ConsumptionResult>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/inventory/{produced.Holding.Id}/use", new ConsumptionRequest(produced.Holding.Version, true, "say"));
        Assert.Equal(message, used.Message); Assert.Equal(1, used.Holding.Quantity);
        using var request = Request(HttpMethod.Patch, $"/api/v2/communities/{g.Id}/item-definitions/{recipe.Id}", new DefinitionWrite("potion", recipe.Name, recipe.CategoryId, recipe.Description, true, new(1, "drinks <3 <t>")), etag: $"\"definition:{recipe.Id:N}:{recipe.Revision}\"");
        using var rejected = await c.SendAsync(request, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        var error = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken); Assert.Contains("invalid_message", error); Assert.Contains("placeholders", error);
        var retained = await c.GetFromJsonAsync<Definition>($"/api/v2/communities/{g.Id}/item-definitions/{recipe.Id}", TestContext.Current.CancellationToken); Assert.Equal(recipe.Revision, retained!.Revision); Assert.Equal(message, retained.Potion.UseMessage);
    }

    [Fact]
    public async Task Recipe_text_limits_are_discovered_and_enforced_with_field_specific_feedback()
    {
        using var root = Factory(); using var f = root.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ServicePolicy:DefinitionNameTextElements"] = "8", ["ServicePolicy:DefinitionDescriptionTextElements"] = "12", ["ServicePolicy:ChatMessageTextElements"] = "10"
        })));
        await Migrate(f); using var c = f.CreateClient(); await Login(c, "501"); var g = await Create(c);
        var capabilities = await c.GetFromJsonAsync<CapabilitiesResponse>("/api/v2/capabilities", TestContext.Current.CancellationToken);
        Assert.Equal(8, capabilities!.Limits.DefinitionNameTextElements); Assert.Equal(12, capabilities.Limits.DefinitionDescriptionTextElements); Assert.Equal(10, capabilities.Limits.ChatMessageTextElements);
        var cats = await c.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{g.Id}/categories", TestContext.Current.CancellationToken); var category = cats!.Items.Single(x => x.AllowedTypeIds.Contains("potion")).Id;
        var valid = new DefinitionWrite("potion", "Healing", category, "Modest.", true, new(1, "uses <3"));
        await Command<Definition>(c, HttpMethod.Post, $"/api/v2/communities/{g.Id}/item-definitions", valid);
        foreach (var (body, field) in new[] { (valid with { Name = "Too long name" }, "Item name"), (valid with { Description = new string('a', 13) }, "Item description") })
        {
            using var request = Request(HttpMethod.Post, $"/api/v2/communities/{g.Id}/item-definitions", body); using var response = await c.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); Assert.Contains(field, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }
}
