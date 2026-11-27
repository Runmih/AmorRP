using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Seasons;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Class_skill_is_granted_by_selection_and_a_lethal_hit_consumes_stay_standing_once()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21800");
        var season = await Create(owner);
        var root = $"/api/v2/seasons/{season.Id}";
        var list = root + "/combat/skills";
        var cast = root + "/combat/skill-uses";
        using var target = factory.CreateClient();
        var targetId = (await Login(target, "21801")).Character.Id;
        await Enroll(target, season);

        var skill = new CombatSkill(Guid.NewGuid(), "Heavy strike", "", "skill", "strike", "selected",
            "automatic", "STR", 12, "damage", 20, 1, null);
        var heal = new CombatSkill(Guid.NewGuid(), "First Aid", "", "spell", "heal", "selected",
            "automatic", "WIS", 12, "healing", 4, 1, null);
        var library = await Command<SkillLibrary>(owner, HttpMethod.Put, list, new SkillLibrary(1, [skill, heal]));
        using (var hidden = await target.GetAsync(list, ct)) Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        var use = new SkillUse(skill.Id, library.Version, targetId);
        using (var request = Request(HttpMethod.Post, cast, use))
        using (var denied = await owner.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var striker = new SeasonClass(Guid.NewGuid(), "Striker", "", [], [skill.Id, heal.Id]);
        var sentinel = new SeasonClass(Guid.NewGuid(), "Sentinel", "", [], DownedBehavior: "stay_standing");
        var policy = await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/policy",
            new ClassPolicyEdit(1, new(1, 1000, [striker, sentinel])));
        await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/selection",
            new ClassSelectionEdit(policy.PolicyVersion, 1, [striker.Id], 0));
        await Command<ClassSelection>(target, HttpMethod.Put, root + "/classes/selection",
            new ClassSelectionEdit(policy.PolicyVersion, 1, [sentinel.Id], 0));
        var sheet = (await owner.GetFromJsonAsync<CombatSheet>(root + "/combat", ct))!;
        Assert.Equal(2, sheet.Skills!.Length);
        using (var request = Request(HttpMethod.Put, list, library with { Skills = [heal] }))
        using (var conflict = await owner.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var key = Guid.CreateVersion7(clock.UtcNow);
        SkillResult first;
        using (var request = Request(HttpMethod.Post, cast, use, key))
        using (var response = await owner.SendAsync(request, ct))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            first = (await response.Content.ReadFromJsonAsync<CommandResult<SkillResult>>(ct))!.Result;
        }
        Assert.True(first.StayedStanding);
        Assert.Equal(1, first.RemainingHp);
        using (var request = Request(HttpMethod.Post, cast, use, key))
        using (var response = await owner.SendAsync(request, ct))
        {
            Assert.True(response.Headers.Contains("Idempotency-Replayed"));
            Assert.Equal(first, (await response.Content.ReadFromJsonAsync<CommandResult<SkillResult>>(ct))!.Result);
        }
        var second = await Command<SkillResult>(owner, HttpMethod.Post, cast, use);
        Assert.False(second.StayedStanding);
        Assert.Equal(0, second.RemainingHp);
        var healed = await Command<SkillResult>(owner, HttpMethod.Post, cast, new SkillUse(heal.Id, library.Version, targetId));
        Assert.Equal(4, healed.RemainingHp);
        Assert.Equal(4, healed.Amount);
    }

    [Fact]
    public async Task Class_crafting_bonuses_take_the_highest_and_member_restrictions_win_without_refilling_usage()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21810");
        var season = await Create(owner);
        var recipe = await Recipe(owner, season);
        var root = $"/api/v2/seasons/{season.Id}";
        using var member = factory.CreateClient();
        var id = (await Login(member, "21811")).Character.Id;
        await Enroll(member, season);
        const string grant = "items.potion.create";
        async Task<Quota> QuotaAsync() => (await member.GetFromJsonAsync<Quotas>(root + "/quotas", ct))!.Items.Single(quota => quota.Kind == "potion_points");

        var baseline = (await QuotaAsync()).Limit;
        var novice = new SeasonClass(Guid.NewGuid(), "Apprentice", "", [grant], Crafting: new(Alchemy: 4));
        var master = new SeasonClass(Guid.NewGuid(), "Journeyman", "", [grant], Crafting: new(Alchemy: 7));
        var policy = await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/policy",
            new ClassPolicyEdit(1, new(2, 1000, [novice, master])));
        var selected = await Command<ClassSelection>(member, HttpMethod.Put, root + "/classes/selection",
            new ClassSelectionEdit(policy.PolicyVersion, 1, [novice.Id], 0));
        var requestBody = new PotionCreate(recipe.Id, recipe.Revision, 1);
        var production = await Command<ProductionResult>(member, HttpMethod.Post, root + "/inventory/potions", requestBody);
        Assert.Equal(baseline + 4, production.Quota.Limit);
        await Command<ClassSelection>(member, HttpMethod.Put, root + "/classes/selection",
            new ClassSelectionEdit(policy.PolicyVersion, selected.SelectionVersion, [novice.Id, master.Id], 0));
        var combined = await QuotaAsync();
        Assert.Equal(baseline + 7, combined.Limit);
        Assert.Equal(production.Quota.Used, combined.Used);

        var row = await Member(owner, season, id);
        await Command<Member>(owner, HttpMethod.Put, root + $"/members/{id}/capabilities",
            new MemberCapabilities([grant], BlockedGameplayGrants: [grant]), row.Etag);
        var restricted = await QuotaAsync();
        Assert.Equal(baseline, restricted.Limit);
        Assert.Equal(combined.Used, restricted.Used);
        using var request = Request(HttpMethod.Post, root + "/inventory/potions", requestBody);
        using var denied = await member.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
