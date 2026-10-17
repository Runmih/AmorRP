using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Inventory;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Community_and_recipe_descriptions_enforce_three_hundred_unicode_characters()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient(); await Login(client, "795");
        var text = string.Concat(Enumerable.Repeat("🌸", 300));
        var community = await Command<Community>(client, HttpMethod.Post, "/api/v2/communities", new CommunityCreate("Limit community", text, "Coins", "C", 12, 5));
        Assert.Equal(text, community.Description);
        using var create = Request(HttpMethod.Post, "/api/v2/communities", new CommunityCreate("Too long", text + "x", "Coins", "C", 12, 5));
        using var rejectedCreate = await client.SendAsync(create, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedCreate.StatusCode);
        using var patch = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}", new CommunityEdit(community.Name, text + "x"), etag: $"\"community:{community.Id:N}:{community.Version}\"");
        using var rejectedPatch = await client.SendAsync(patch, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedPatch.StatusCode);
        var recipe = await Recipe(client, community);
        var edit = new DefinitionWrite("potion", recipe.Name, recipe.CategoryId, text, true, recipe.Potion);
        var updated = await Command<Definition>(client, HttpMethod.Patch, $"/api/v2/communities/{community.Id}/item-definitions/{recipe.Id}", edit, $"\"definition:{recipe.Id:N}:{recipe.Revision}\"");
        using var tooLong = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}/item-definitions/{recipe.Id}", edit with { Description = text + "x" }, etag: $"\"definition:{recipe.Id:N}:{updated.Revision}\"");
        using var rejectedRecipe = await client.SendAsync(tooLong, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedRecipe.StatusCode);
        var capabilities = await client.GetFromJsonAsync<CapabilitiesResponse>("/api/v2/capabilities", TestContext.Current.CancellationToken);
        Assert.Equal(300, capabilities!.Limits.DefinitionDescriptionTextElements); Assert.Equal(500, capabilities.Limits.LetterBodyTextElements);
    }

    [Fact]
    public async Task All_Books_types_reject_oversized_creation_and_edit_without_spending_extra_allowance()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient(); await Login(client, "796"); var community = await Create(client);
        var categories = await client.GetFromJsonAsync<CategoryPage>($"/api/v2/communities/{community.Id}/categories", TestContext.Current.CancellationToken);
        var body = string.Concat(Enumerable.Repeat("🌸", 500));
        foreach (var type in new[] { "book", "letter", "note" })
        {
            var category = categories!.Items.Single(x => x.AllowedTypeIds.Contains(type));
            var create = new LetterCreate(category.Id, "Boundary " + type, body, type);
            var result = await Command<ProductionResult>(client, HttpMethod.Post, $"/api/v2/communities/{community.Id}/inventory/letters", create);
            var details = await client.GetFromJsonAsync<HoldingDetails>($"/api/v2/communities/{community.Id}/inventory/{result.Holding.Id}", TestContext.Current.CancellationToken);
            var letter = details!.Letter!;
            Assert.Equal(body, letter.Body);
            using var tooLong = Request(HttpMethod.Post, $"/api/v2/communities/{community.Id}/inventory/letters", create with { Body = body + "x" });
            using var rejected = await client.SendAsync(tooLong, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
            using var edit = Request(HttpMethod.Patch, $"/api/v2/communities/{community.Id}/letters/{letter.Id}", new LetterEdit(letter.Title, body + "x"), etag: $"\"letter:{letter.Id:N}:{letter.Version}\"");
            using var rejectedEdit = await client.SendAsync(edit, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedEdit.StatusCode);
        }
        var quota = await client.GetFromJsonAsync<Quotas>($"/api/v2/communities/{community.Id}/quotas", TestContext.Current.CancellationToken);
        Assert.Equal(3, quota!.Items.Single(x => x.Kind == "letters").Used);
    }
}
