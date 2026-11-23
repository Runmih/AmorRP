using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Seasons;
using AmorRP.Core.Items;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Class_reset_debits_once_and_preserves_explicit_permissions_and_weekly_usage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21400");
        var season = await Create(owner);
        var recipe = await Recipe(owner, season);
        var root = $"/api/v2/seasons/{season.Id}";
        using var member = factory.CreateClient();
        var memberId = (await Login(member, "21401")).Character.Id;
        await Enroll(member, season);
        await Fund(owner, season, memberId, "1200");
        var row = await Member(owner, season, memberId);
        await Command<Member>(owner, HttpMethod.Put, root + $"/members/{memberId}/capabilities",
            new MemberCapabilities(["items.cooking.create"]), row.Etag);

        var alchemist = new SeasonClass(Guid.NewGuid(), "Alchemist", "", ["items.potion.create"]);
        var gatherer = new SeasonClass(Guid.NewGuid(), "Gatherer", "", [CraftingTypes.GatherPermission]);
        var policy = await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/policy", new ClassPolicyEdit(1,
            new(1, 1000, [alchemist, gatherer])));
        var original = await Command<ClassSelection>(member, HttpMethod.Put, root + "/classes/selection",
            new ClassSelectionEdit(policy.PolicyVersion, 1, [alchemist.Id], 0));
        await Command<ProductionResult>(member, HttpMethod.Post, root + "/inventory/potions", new PotionCreate(recipe.Id, recipe.Revision, 1));
        var swap = new ClassSelectionEdit(original.PolicyVersion, original.SelectionVersion, [gatherer.Id], 1000);
        using (var req = Request(HttpMethod.Put, root + "/classes/selection", swap with { ConfirmedPrice = 0 }))
        using (var denied = await member.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        var key = Guid.CreateVersion7(clock.UtcNow);
        for (var i = 0; i < 2; i++)
        {
            using var req = Request(HttpMethod.Put, root + "/classes/selection", swap, key);
            using var response = await member.SendAsync(req, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(i == 1, response.Headers.Contains("Idempotency-Replayed"));
        }
        var updated = await Enroll(member, season);
        Assert.DoesNotContain("items.potion.create", updated.MyCapabilities);
        Assert.Contains("items.cooking.create", updated.MyCapabilities);
        Assert.Contains(CraftingTypes.GatherPermission, updated.MyCapabilities);
        Assert.Equal(new[] { "items.cooking.create" }, (await Member(owner, season, memberId)).Capabilities);
        Assert.Equal("200", (await OwnBalance(member, season)).Owned);
        var quotas = (await member.GetFromJsonAsync<Quotas>(root + "/quotas", ct))!;
        Assert.Equal(3, quotas.Items.Single(q => q.Kind == "potion_points").Used);
        Assert.Contains(quotas.Items, q => q.Kind == "gathering_points");

        var changed = await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/policy",
            new ClassPolicyEdit(policy.PolicyVersion, policy.Policy with { Classes = [alchemist] }));
        var correction = (await member.GetFromJsonAsync<ClassSelection>(root + "/classes", ct))!;
        Assert.True(correction.FreeCorrection);
        await Command<ClassSelection>(member, HttpMethod.Put, root + "/classes/selection",
            new ClassSelectionEdit(changed.PolicyVersion, correction.SelectionVersion, [alchemist.Id], 0));
        Assert.Equal("200", (await OwnBalance(member, season)).Owned);
    }

    [Fact]
    public async Task Group_recipe_requires_membership_and_permission_then_disappears_after_leaving()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21410");
        var season = await Create(owner);
        var publicRecipe = await Recipe(owner, season);
        var root = $"/api/v2/seasons/{season.Id}";
        var group = await Command<GroupProfile>(owner, HttpMethod.Post, root + "/groups", new GroupWrite("Kitchen", ""));
        await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/policy", new ClassPolicyEdit(1, new(1, 1000, [], 1)));
        var draft = new DefinitionWrite("potion", "Private potion", publicRecipe.CategoryId, "", true, new(1, "drinks a potion."), group.Id);
        var privateRecipe = await Command<Definition>(owner, HttpMethod.Post, root + "/item-definitions", draft);
        using (var req = Request(HttpMethod.Post, root + "/item-definitions", draft with { Name = "Another" }))
        using (var denied = await owner.SendAsync(req, ct))
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);

        using var member = factory.CreateClient();
        var memberId = (await Login(member, "21411")).Character.Id;
        await Enroll(member, season);
        var produce = new PotionCreate(privateRecipe.Id, privateRecipe.Revision, 1);
        var detail = root + "/item-definitions/" + privateRecipe.Id;
        using (var hidden = await member.GetAsync(detail, ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var req = Request(HttpMethod.Post, root + "/inventory/potions", produce))
        using (var denied = await member.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        await Command<Receipt>(member, HttpMethod.Post, root + $"/groups/{group.Id}/join", new GroupJoin());
        Assert.DoesNotContain((await member.GetFromJsonAsync<DefinitionPage>(root + "/item-definitions", ct))!.Items, x => x.Id == privateRecipe.Id);
        using (var req = Request(HttpMethod.Post, root + "/inventory/potions", produce))
        using (var denied = await member.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await GrantPotions(owner, season, memberId);
        Assert.Contains((await member.GetFromJsonAsync<DefinitionPage>(root + "/item-definitions", ct))!.Items, x => x.Id == privateRecipe.Id);
        var crafted = await Command<ProductionResult>(member, HttpMethod.Post, root + "/inventory/potions", produce);
        await Command<Receipt>(member, HttpMethod.Delete, root + $"/groups/{group.Id}/membership");
        using (var hidden = await member.GetAsync(detail, ct)) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var req = Request(HttpMethod.Post, root + "/inventory/potions", produce))
        using (var denied = await member.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(privateRecipe.Name, (await member.GetFromJsonAsync<HoldingDetails>(root + "/inventory/" + crafted.Holding.Id, ct))!.Definition!.Name);
    }

    [Fact]
    public async Task Consuming_identical_effects_refreshes_one_grant_and_pinned_items_keep_their_payload()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21420");
        var season = await Create(owner);
        var category = (await Recipe(owner, season)).CategoryId;
        var root = $"/api/v2/seasons/{season.Id}";
        var effect = new MoodleEffect("Warmth", "Feels warm.", 200001, 20, false);
        var draft = new DefinitionWrite("potion", "Warming potion", category, "", true, new(1, "drinks a warming potion."), Effect: effect);
        var recipe = await Command<Definition>(owner, HttpMethod.Post, root + "/item-definitions", draft);
        var holding = (await Command<ProductionResult>(owner, HttpMethod.Post, root + "/inventory/potions",
            new PotionCreate(recipe.Id, recipe.Revision, 2))).Holding;
        await Command<Definition>(owner, HttpMethod.Patch, root + $"/item-definitions/{recipe.Id}",
            draft with { Effect = effect with { Name = "A new effect" } }, $"\"definition:{recipe.Id:N}:{recipe.Revision}\"");
        var usePath = root + $"/inventory/{holding.Id}/use";
        using (var req = Request(HttpMethod.Post, usePath, new ConsumptionRequest(holding.Version, false, "say"), Guid.CreateVersion7(clock.UtcNow)))
        using (var response = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var used = await Command<ConsumptionResult>(owner, HttpMethod.Post, usePath, new ConsumptionRequest(holding.Version, false, ""));
        Assert.Equal(1, used.Holding.Quantity);
        Assert.Empty(used.Destination);
        Assert.Equal("none", used.ChatDelivery);
        var first = Assert.Single((await owner.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!.Active);
        Assert.Equal(effect, first.Effect);
        clock.UtcNow = clock.UtcNow.AddSeconds(10);
        var key = Guid.CreateVersion7(clock.UtcNow);
        var useAgain = new ConsumptionRequest(used.Holding.Version, true, "say");
        using (var req = Request(HttpMethod.Post, usePath, useAgain, key))
        using (var response = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = Assert.Single((await owner.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!.Active);
        Assert.Equal(first.Id, refreshed.Id);
        Assert.Equal(first.ExpiresAt!.Value.AddSeconds(10), refreshed.ExpiresAt);
        clock.UtcNow = clock.UtcNow.AddSeconds(5);
        using (var req = Request(HttpMethod.Post, usePath, useAgain, key))
        using (var response = await owner.SendAsync(req, ct)) Assert.True(response.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(refreshed, Assert.Single((await owner.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!.Active));
        clock.UtcNow = clock.UtcNow.AddSeconds(16);
        Assert.Empty((await owner.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!.Active);
    }
}
