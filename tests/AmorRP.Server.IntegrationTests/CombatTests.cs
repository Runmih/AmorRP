using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Seasons;
using AmorRP.Core.Combat;
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
    public async Task Potion_points_are_pinned_to_the_holding_and_reset_preserves_the_earned_budget()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        var ownerId = (await Login(owner, "21710")).Character.Id;
        var season = await Create(owner);
        var root = $"/api/v2/seasons/{season.Id}";
        var combat = root + "/combat";
        var policy = (await owner.GetFromJsonAsync<CombatPolicy>(combat + "/policy", ct))!;
        await Command<CombatPolicy>(owner, HttpMethod.Put, combat + "/policy", policy with { StatPoints = 4 });
        var initial = (await owner.GetFromJsonAsync<CombatSheet>(combat, ct))!;
        Assert.Equal(4, initial.AvailablePoints);
        var allocated = await Command<CombatSheet>(owner, HttpMethod.Post, combat + "/stats",
            new CombatAllocation(initial.Version, [4, 0, 0, 0, 0, 0]));
        Assert.Equal(12, allocated.Scores[0]);
        using (var req = Request(HttpMethod.Post, combat + "/stats", new CombatAllocation(allocated.Version, [-4, 4, 0, 0, 0, 0])))
        using (var response = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var category = (await Recipe(owner, season)).CategoryId;
        var write = new DefinitionWrite("potion", "Stat Potion", category, "", true, new(1, "drinks a training potion."), StatPoints: 2);
        var definition = await Command<Definition>(owner, HttpMethod.Post, root + "/item-definitions", write);
        var item = (await Command<ProductionResult>(owner, HttpMethod.Post, root + "/inventory/potions",
            new PotionCreate(definition.Id, definition.Revision, 1))).Holding;
        await Command<Definition>(owner, HttpMethod.Patch, root + "/item-definitions/" + definition.Id,
            write with { StatPoints = 7 }, $"\"definition:{definition.Id:N}:{definition.Revision}\"");
        await Command<ConsumptionResult>(owner, HttpMethod.Post, root + $"/inventory/{item.Id}/use", new ConsumptionRequest(item.Version, false, ""));
        var trained = (await owner.GetFromJsonAsync<CombatSheet>(combat, ct))!;
        Assert.Equal(2, trained.AvailablePoints);
        Assert.Equal(2, trained.BonusPoints);

        var shop = root + "/shop/reset-potion";
        var listing = (await owner.GetFromJsonAsync<ResetPotionProduct>(shop, ct))!;
        var configured = await Command<ResetPotionProduct>(owner, HttpMethod.Put, shop, new ResetPotionSettings(listing.Version, 30, 1));
        await Fund(owner, season, ownerId, "100");
        await Command<ResetPotionProduct>(owner, HttpMethod.Post, shop + "/purchases", new ResetPotionBuy(configured.Version));
        using (var req = Request(HttpMethod.Post, shop + "/purchases", new ResetPotionBuy(configured.Version)))
        using (var response = await owner.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var reset = await Command<CombatSheet>(owner, HttpMethod.Post, combat + "/reset");
        Assert.All(reset.Scores, score => Assert.Equal(8, score));
        Assert.Equal(6, reset.AvailablePoints);
        Assert.Equal(0, reset.ResetPotions);
        Assert.Equal("70", (await OwnBalance(owner, season)).Owned);
    }

    [Fact]
    public async Task Point_buy_validates_escalating_costs_and_updates_hp_from_constitution()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        var id = (await Login(owner, "21730")).Character.Id;
        var season = await Create(owner);
        var path = $"/api/v2/seasons/{season.Id}/combat";
        var initial = (await owner.GetFromJsonAsync<CombatSheet>(path, ct))!;
        Assert.Equal(27, initial.AvailablePoints);
        Assert.Equal(19, initial.HitPoints);
        var injured = await Command<CombatSheet>(owner, HttpMethod.Put, path + "/health/" + id, new CombatHealthEdit(15));
        var built = await Command<CombatSheet>(owner, HttpMethod.Post, path + "/stats",
            new CombatAllocation(injured.Version, [7, 7, 7, 0, 0, 0]));
        Assert.Equal(0, built.AvailablePoints);
        Assert.Equal(22, built.MaxHitPoints);
        Assert.Equal(18, built.HitPoints);
        using var request = Request(HttpMethod.Post, path + "/stats", new CombatAllocation(built.Version, [1, 0, 0, 0, 0, 0]));
        using var denied = await owner.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, denied.StatusCode);
        var restored = await Command<CombatSheet>(owner, HttpMethod.Put, path + "/health/" + id, new CombatHealthEdit(0, RestoreFull: true));
        Assert.Equal(22, restored.HitPoints);
    }

    [Fact]
    public async Task Point_buy_migration_preserves_purchased_scores_wounds_and_weapon_revisions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var owner = factory.CreateClient();
        var id = (await Login(owner, "21731")).Character.Id;
        var season = await Create(owner);
        var root = $"/api/v2/seasons/{season.Id}";
        var category = Assert.Single((await owner.GetFromJsonAsync<CategoryPage>(root + "/categories", ct))!.Items,
            item => item.AllowedTypeIds.Contains("weapon"));
        var recipe = await Command<Definition>(owner, HttpMethod.Post, root + "/item-definitions",
            new DefinitionWrite("weapon", "Old staff", category.Id, "", true, new(1, ""), Gear: new("melee", "DEX", 0, 1, 9)));
        var item = (await Command<ProductionResult>(owner, HttpMethod.Post, root + "/inventory/potions",
            new PotionCreate(recipe.Id, recipe.Revision, 1))).Holding;
        await Command<CombatSheet>(owner, HttpMethod.Put, root + "/combat/equipment", new CombatEquip(1, "weapon", item.Id));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261124201520_CombatAndCharacterProgression", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE combat_sheets SET "Allocated" = ARRAY[6,0,4,0,0,0], "HitPoints" = 13
            WHERE "CommunityId" = {season.Id} AND "CharacterId" = {id}
            """, ct);
        const string oldGear = """{"Form":"staff","Ability":"DEX","Value":5}""";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE definition_revisions SET "GearJson" = {oldGear} WHERE "DefinitionId" = {recipe.Id}
            """, ct);
        await db.Database.MigrateAsync(ct);

        var upgraded = (await owner.GetFromJsonAsync<CombatSheet>(root + "/combat", ct))!;
        Assert.Equal(new[] { 16, 8, 14, 8, 8, 8 }, upgraded.Scores);
        Assert.Equal(22, upgraded.MaxHitPoints);
        Assert.Equal(15, upgraded.HitPoints);
        Assert.Equal(new CombatGear("melee", "DEX", 0, 1, 9), upgraded.Weapon!.Gear);
    }

    [Fact]
    public async Task Combat_uses_equipped_weapon_and_armor_and_downing_is_server_owned()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        await Migrate(factory);
        using var attacker = factory.CreateClient();
        var attackerId = (await Login(attacker, "21720")).Character.Id;
        var season = await Create(attacker);
        var root = $"/api/v2/seasons/{season.Id}";
        var path = root + "/combat";
        var downed = new MoodleEffect("Downed", "Needs recovery.", 200001, 90, true);
        var policy = (await attacker.GetFromJsonAsync<CombatPolicy>(path + "/policy", ct))!;
        await Command<CombatPolicy>(attacker, HttpMethod.Put, path + "/policy", policy with { DownedEffect = downed });
        var category = Assert.Single((await attacker.GetFromJsonAsync<CategoryPage>(root + "/categories", ct))!.Items,
            c => c.AllowedTypeIds.Contains("weapon"));
        var gear = await Command<Definition>(attacker, HttpMethod.Post, root + "/item-definitions",
            new DefinitionWrite("weapon", "Practice sword", category.Id, "", true, new(1, ""), Gear: new("melee", "WIS", 0, 1, 6)));
        var made = await Command<ProductionResult>(attacker, HttpMethod.Post, root + "/inventory/potions",
            new PotionCreate(gear.Id, gear.Revision, 1));
        Assert.Equal(CombatRules.BlacksmithingQuota, made.Quota.Kind);
        var equipped = await Command<CombatSheet>(attacker, HttpMethod.Put, path + "/equipment", new CombatEquip(1, "weapon", made.Holding.Id));
        Assert.Equal("WIS", equipped.Weapon!.Gear.Ability);

        using var target = factory.CreateClient();
        var targetId = (await Login(target, "21721")).Character.Id;
        var row = await Member(attacker, season, targetId);
        await Command<Member>(attacker, HttpMethod.Put, root + $"/members/{targetId}/capabilities",
            new MemberCapabilities(["items.blacksmith.create"], CombatAllowed: false), row.Etag);
        var armor = await Command<Definition>(attacker, HttpMethod.Post, root + "/item-definitions",
            new DefinitionWrite("armor", "Practice armor", category.Id, "", true, new(1, ""), Gear: new("armor", null, 13)));
        var suit = (await Command<ProductionResult>(target, HttpMethod.Post, root + "/inventory/potions",
            new PotionCreate(armor.Id, armor.Revision, 1))).Holding;
        await Command<CombatSheet>(target, HttpMethod.Put, path + "/equipment", new CombatEquip(1, "armor", suit.Id));
        var attackKey = Guid.CreateVersion7(clock.UtcNow);
        using var original = Request(HttpMethod.Post, path + "/attacks", new CombatAttack(targetId), attackKey);
        using var applied = await attacker.SendAsync(original, ct);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var attack = (await applied.Content.ReadFromJsonAsync<CommandResult<CombatHit>>(ct))!.Result;
        using var retry = Request(HttpMethod.Post, path + "/attacks", new CombatAttack(targetId), attackKey);
        using var replay = await attacker.SendAsync(retry, ct);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(attack, (await replay.Content.ReadFromJsonAsync<CommandResult<CombatHit>>(ct))!.Result);
        Assert.Equal(attack.RemainingHp, (await target.GetFromJsonAsync<CombatSheet>(path, ct))!.HitPoints);
        Assert.Equal(13, attack.ArmorClass);
        Assert.Equal(2, attack.Roll.Proficiency);
        Assert.Equal(CombatRules.Hits(attack.Roll.Die, attack.Roll.Total, 13), attack.Hit);
        Assert.Equal(CombatRules.StartingHitPoints - attack.Damage, attack.RemainingHp);
        if (attack.Hit)
        {
            Assert.Equal(attack.Critical ? 2 : 1, attack.DamageDiceCount);
            Assert.Equal(6, attack.DamageDiceSides);
            Assert.InRange(attack.RolledDamage, attack.DamageDiceCount, attack.DamageDiceCount * 6);
            Assert.Equal(CombatRules.Damage(attack.RolledDamage, attack.Roll.Modifier), attack.Damage);
        }
        using (var req = Request(HttpMethod.Post, path + "/attacks", new CombatAttack(attackerId)))
        using (var denied = await target.SendAsync(req, ct)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Command<CombatSheet>(attacker, HttpMethod.Put, path + "/health/" + targetId, new CombatHealthEdit(0));
        var granted = Assert.Single((await target.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!.Active);
        Assert.Equal(downed, granted.Effect);
        await Command<CombatSheet>(attacker, HttpMethod.Put, path + "/health/" + targetId, new CombatHealthEdit(0, RestoreFull: true));
        var cleared = (await target.GetFromJsonAsync<MoodleGrants>("/api/v2/me/moodle-effects", ct))!;
        Assert.Contains(granted.Id, cleared.Remove);
        Assert.Empty(cleared.Active);
        await Command<CombatSheet>(attacker, HttpMethod.Put, path + "/health/" + attackerId, new CombatHealthEdit(0));
        using var downedAttack = Request(HttpMethod.Post, path + "/attacks", new CombatAttack(targetId));
        using var deniedDowned = await attacker.SendAsync(downedAttack, ct);
        Assert.Equal(HttpStatusCode.Conflict, deniedDowned.StatusCode);
    }
}
