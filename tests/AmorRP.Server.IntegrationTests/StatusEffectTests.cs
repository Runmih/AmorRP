using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Status_skills_apply_on_success_and_only_the_strongest_bonus_and_penalty_count()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        await Login(owner, "21920");
        var season = await Create(owner);
        using var target = factory.CreateClient();
        var targetId = (await Login(target, "21921")).Character.Id;
        await Enroll(target, season);
        var root = $"/api/v2/seasons/{season.Id}";
        var statusPath = root + "/status-effects";
        var abilities = new List<CombatSkill>();
        foreach (var (name, amount) in new[] { ("Inspired", 2), ("Blessed", 4), ("Shaken", -1), ("Cursed", -3) })
        {
            var id = Guid.NewGuid();
            var draft = new StatusEffect(id, 0, name, 90, "check", amount, "STR", null);
            using (var request = Request(HttpMethod.Put, statusPath + "/" + id, draft))
            using (var denied = await target.SendAsync(request, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            var effect = await Command<StatusEffect>(owner, HttpMethod.Put, statusPath + "/" + id, draft);
            abilities.Add(new(Guid.NewGuid(), name, "", "spell", "spark", "selected", "automatic", "WIS", 15,
                "status", 0, 0, null, effect.Reference()));
        }
        var listed = (await target.GetFromJsonAsync<StatusEffect[]>(statusPath, ct))!;
        Assert.Equal(4, listed.Length);
        var safeDc = abilities[0] with { Id = Guid.NewGuid(), Name = "Harmless spell", Resolution = "save", RollAbility = "CON", SaveDc = 1 };
        abilities.Add(safeDc);
        var skillLibrary = await Command<SkillLibrary>(owner, HttpMethod.Put, root + "/combat/skills", new SkillLibrary(1, abilities.ToArray()));
        var caster = new SeasonClass(Guid.NewGuid(), "Caster", "", [], abilities.Select(skill => skill.Id).ToArray());
        var policy = await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/policy",
            new ClassPolicyEdit(1, new(1, 1000, [caster])));
        await Command<ClassSelection>(owner, HttpMethod.Put, root + "/classes/selection",
            new ClassSelectionEdit(policy.PolicyVersion, 1, [caster.Id], 0));
        var before = (await target.GetFromJsonAsync<CombatSheet>(root + "/combat", ct))!;
        await Command<CombatSheet>(target, HttpMethod.Post, root + "/combat/stats",
            new CombatAllocation(before.Version, [0, 0, 2, 0, 0, 0]));
        var resisted = await Command<SkillResult>(owner, HttpMethod.Post, root + "/combat/skill-uses",
            new SkillUse(safeDc.Id, skillLibrary.Version, targetId));
        Assert.False(resisted.Applied);
        Assert.Null(resisted.AppliedStatus);
        foreach (var skill in abilities.Take(4))
        {
            var used = await Command<SkillResult>(owner, HttpMethod.Post, root + "/combat/skill-uses",
                new SkillUse(skill.Id, skillLibrary.Version, targetId));
            Assert.Equal(skill.Name, used.AppliedStatus);
            Assert.Equal(0, used.Amount);
        }

        var roll = await Command<CombatRoll>(target, HttpMethod.Post, root + "/combat/checks", new CombatCheck("STR"));
        Assert.Equal(1, roll.StatusModifier);
        Assert.Equal(roll.Die + roll.Modifier + 1, roll.Total);
        var other = await Command<CombatRoll>(target, HttpMethod.Post, root + "/combat/checks", new CombatCheck("DEX"));
        Assert.Equal(0, other.StatusModifier);
        Assert.Empty((await target.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!.Active);
    }

    [Fact]
    public async Task Status_migration_preserves_old_recipe_and_active_moodle_id_and_expiry()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var client = factory.CreateClient();
        var actor = (await Login(client, "21922")).Character.Id;
        var season = await Create(client);
        var recipe = await Recipe(client, season);
        var grantId = Guid.NewGuid();
        var expires = DateTimeOffset.FromUnixTimeSeconds(clock.UtcNow.ToUnixTimeSeconds()).AddMinutes(3);
        const string oldPayload = """{"Name":"Old warmth","Description":"Warm","IconId":200001,"DurationSeconds":300,"Debuff":false}""";
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261126151959_ClassSkillLibrary", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE definition_revisions SET "MoodleEffectJson" = {oldPayload} WHERE "DefinitionId" = {recipe.Id};
            INSERT INTO moodle_grants ("CommunityId", "CharacterId", "Id", "EffectJson", "ExpiresAt")
            VALUES ({season.Id}, {actor}, {grantId}, {oldPayload}, {expires});
            """, ct);
        await db.Database.MigrateAsync(ct);
        var root = $"/api/v2/seasons/{season.Id}";
        var status = Assert.Single((await client.GetFromJsonAsync<StatusEffect[]>(root + "/status-effects", ct))!);
        var definition = (await client.GetFromJsonAsync<Definition>(root + $"/item-definitions/{recipe.Id}", ct))!;
        Assert.Equal(status, definition.StatusEffect);
        Assert.Equal("display", status.Modifier);
        var active = Assert.Single((await client.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!.Active);
        Assert.Equal(grantId, active.Id);
        Assert.Equal(expires, active.ExpiresAt);
        Assert.Equal(status.Moodle, active.Effect);
    }
}
