using System.Security.Cryptography;
using System.Text.Json;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Common;
using AmorRP.Core.Combat;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.Moodles;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Features.Shop;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Combat;

public sealed class CombatService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons, CommandStore commands)
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/combat", (HttpContext h, Guid seasonId, CombatService s, CancellationToken ct) => s.ReadAsync(h, seasonId, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/combat/policy", (HttpContext h, Guid seasonId, CombatService s, CancellationToken ct) => s.PolicyAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/combat/policy", (HttpContext h, Guid seasonId, CombatPolicy body, CombatService s, CancellationToken ct) => s.SavePolicyAsync(h, seasonId, body, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/combat/stats", (HttpContext h, Guid seasonId, CombatAllocation body, CombatService s, CancellationToken ct) => s.AllocateAsync(h, seasonId, body, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/combat/equipment", (HttpContext h, Guid seasonId, CombatEquip body, CombatService s, CancellationToken ct) => s.EquipAsync(h, seasonId, body, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/combat/attacks", (HttpContext h, Guid seasonId, CombatAttack body, CombatService s, CancellationToken ct) => s.AttackAsync(h, seasonId, body, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/combat/checks", (HttpContext h, Guid seasonId, CombatCheck body, CombatService s, CancellationToken ct) => s.CheckAsync(h, seasonId, body, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/combat/health/{characterId}", (HttpContext h, Guid seasonId, Guid characterId, CombatHealthEdit body, CombatService s, CancellationToken ct) => s.HealthAsync(h, seasonId, characterId, body, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/shop/reset-potion", (HttpContext h, Guid seasonId, CombatService s, CancellationToken ct) => s.ReadPotionAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/shop/reset-potion", (HttpContext h, Guid seasonId, ResetPotionSettings body, CombatService s, CancellationToken ct) => s.SavePotionAsync(h, seasonId, body, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/shop/reset-potion/purchases", (HttpContext h, Guid seasonId, ResetPotionBuy body, CombatService s, CancellationToken ct) => s.BuyPotionAsync(h, seasonId, body, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/combat/reset", (HttpContext h, Guid seasonId, CombatService s, CancellationToken ct) => s.UsePotionAsync(h, seasonId, ct));
    }

    public static CombatGear? Gear(string? json) => json == null ? null : JsonSerializer.Deserialize<CombatGear>(json);

    public static string? EncodeGear(string type, CombatGear? gear)
    {
        if (!CraftingTypes.IsGear(type))
        {
            ApiFault.Require(gear == null, 422, "invalid_gear", "Only Blacksmithing recipes can define equipment.");
            return null;
        }
        ApiFault.Require(type == "weapon"
            ? gear is { Form: "sword" or "staff", Ability: "STR" or "DEX", Value: >= 1 and <= 100 }
            : gear is { Form: "armor", Ability: null, Value: >= 10 and <= 25 },
            422, "invalid_gear", "Choose valid weapon Power and an ability, or an armor class from 10 to 25.");
        return JsonSerializer.Serialize(gear);
    }

    private async Task<CombatSheetRow> SheetAsync(Guid season, Guid character, bool write, CancellationToken ct)
    {
        var sheet = await db.CombatSheets.FindAsync([season, character], ct);
        if (sheet != null) return sheet;
        sheet = new CombatSheetRow { CommunityId = season, CharacterId = character };
        if (write) db.CombatSheets.Add(sheet);
        return sheet;
    }

    public async Task GrantPointsAsync(Guid season, Guid character, int amount, CancellationToken ct)
    {
        var sheet = await SheetAsync(season, character, true, ct);
        ApiFault.Require(amount > 0 && sheet.BonusPoints <= CombatRules.MaximumBonusPoints - amount, 409,
            "stat_point_limit", "This potion would exceed your permanent bonus point limit.");
        sheet.BonusPoints += amount;
        sheet.Version++;
    }

    private static int Available(CommunityRow season, CombatSheetRow sheet) =>
        Math.Max(0, season.StatPointAllowance + sheet.BonusPoints - sheet.Allocated.Sum());

    private async Task<EquippedItem?> GearAsync(CombatSheetRow sheet, Guid? id, string type, CancellationToken ct)
    {
        if (id == null) return null;
        var gear = await (from holding in db.Holdings
            join revision in db.DefinitionRevisions on new { holding.CommunityId, holding.DefinitionId, Revision = holding.DefinitionRevision }
                equals new { revision.CommunityId, DefinitionId = (Guid?)revision.DefinitionId, Revision = (int?)revision.Revision }
            where holding.CommunityId == sheet.CommunityId && holding.Id == id && holding.CharacterId == sheet.CharacterId
                && holding.TypeId == type && holding.Quantity > holding.Reserved
            select new { holding.Name, revision.GearJson }).SingleOrDefaultAsync(ct);
        return Gear(gear?.GearJson) is { } data ? new(id.Value, gear!.Name, data) : null;
    }

    private static int Modifier(CombatSheetRow sheet, string ability)
    {
        var index = CombatRules.Abilities.ToList().IndexOf(ability);
        ApiFault.Require(index >= 0, 422, "unknown_ability", "Choose a supported ability.");
        return CombatRules.AbilityModifier(CombatRules.BaseScore + sheet.Allocated[index]);
    }

    private static int ArmorClass(CombatSheetRow sheet, EquippedItem? armor) => armor?.Gear.Value ?? 10 + Modifier(sheet, "DEX");

    private async Task<CombatSheet> ViewAsync(CommunityRow season, MembershipRow member, CombatSheetRow sheet, CancellationToken ct)
    {
        var weapon = await GearAsync(sheet, sheet.WeaponId, "weapon", ct);
        var armor = await GearAsync(sheet, sheet.ArmorId, "armor", ct);
        return new(sheet.Version, sheet.Allocated.Select(x => CombatRules.BaseScore + x).ToArray(), Available(season, sheet), sheet.BonusPoints,
            sheet.HitPoints, CombatRules.MaxHitPoints, ArmorClass(sheet, armor), weapon, armor, !member.CombatRestricted, sheet.ResetPotions);
    }

    private async Task<IResult> ReadAsync(HttpContext h, Guid id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, member) = await seasons.RequireAsync(id, actor, ct);
        return Results.Json(await ViewAsync(season, member, await SheetAsync(id, actor, false, ct), ct));
    }

    private static CombatPolicy ViewPolicy(CommunityRow season) => new(season.CombatPolicyVersion,
        season.StatPointAllowance, season.BlacksmithingPoints, MoodleService.Decode(season.DownedEffectJson));

    private async Task<IResult> PolicyAsync(HttpContext h, Guid id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, _) = await seasons.RequireAsync(id, actor, ct);
        SeasonAccess.Owner(season, actor);
        return Results.Json(ViewPolicy(season));
    }

    private Task<IResult> SavePolicyAsync(HttpContext h, Guid id, CombatPolicy body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.policy", (season, member, operation) =>
        {
            SeasonAccess.Owner(season, member.CharacterId);
            ApiFault.Require(body.Version == season.CombatPolicyVersion, 409, "combat_policy_changed", "Refresh the combat settings.");
            ApiFault.Require(body.StatPoints is >= 0 and <= 60 && body.BlacksmithingPoints is >= 0 and <= 100000,
                422, "invalid_combat_policy", "Choose a stat allowance up to 60 and a valid Blacksmithing limit.");
            season.StatPointAllowance = body.StatPoints;
            season.BlacksmithingPoints = body.BlacksmithingPoints;
            season.DownedEffectJson = MoodleService.Encode(body.DownedEffect, ownership: true);
            season.CombatPolicyVersion++;
            operation.Summary = "Updated combat rules and the Downed effect.";
            return Task.FromResult(ViewPolicy(season));
        }, ct);

    private Task<IResult> AllocateAsync(HttpContext h, Guid id, CombatAllocation body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.stats", async (season, member, operation) =>
        {
            var sheet = await SheetAsync(id, member.CharacterId, true, ct);
            CheckVersion(sheet, body.Version);
            var points = body.Points;
            ApiFault.Require(points is { Length: 6 } && points.All(x => x is >= 0 and <= 10)
                && points.Sum() > 0 && points.Sum() <= Available(season, sheet)
                && points.Select((amount, i) => CombatRules.BaseScore + sheet.Allocated[i] + amount).All(x => x <= CombatRules.MaximumScore),
                422, "invalid_stat_allocation", "Allocate available points without raising a score above 20.");
            sheet.Allocated = sheet.Allocated.Select((amount, i) => amount + points[i]).ToArray();
            sheet.Version++;
            operation.Summary = $"Allocated {points.Sum()} stat points.";
            return await ViewAsync(season, member, sheet, ct);
        }, ct);

    private static void CheckVersion(CombatSheetRow sheet, int version) =>
        ApiFault.Require(sheet.Version == version, 409, "combat_sheet_changed", "Refresh your character before applying changes.");

    private Task<IResult> EquipAsync(HttpContext h, Guid id, CombatEquip body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.equipment", async (season, member, operation) =>
        {
            var sheet = await SheetAsync(id, member.CharacterId, true, ct);
            CheckVersion(sheet, body.Version);
            ApiFault.Require(CraftingTypes.IsGear(body.Slot), 422, "invalid_slot", "Choose the weapon or armor slot.");
            ApiFault.Require(body.HoldingId == null || await GearAsync(sheet, body.HoldingId, body.Slot, ct) != null,
                409, "gear_unavailable", "Choose an available item in your own inventory.");
            if (body.Slot == "weapon") sheet.WeaponId = body.HoldingId;
            else sheet.ArmorId = body.HoldingId;
            sheet.Version++;
            operation.Summary = "Updated equipped " + body.Slot + ".";
            return await ViewAsync(season, member, sheet, ct);
        }, ct);

    private static void ValidateDestination(string destination) =>
        ApiFault.Require(destination == "" || ItemText.Destination(destination), 422, "invalid_chat_destination",
            "Choose a supported chat channel.");

    private static CombatRoll Roll(CombatSheetRow sheet, string ability, int proficiency = 0)
    {
        var modifier = Modifier(sheet, ability);
        var die = RandomNumberGenerator.GetInt32(1, 21);
        return new(ability, die, modifier, proficiency, die + modifier + proficiency);
    }

    private Task<IResult> CheckAsync(HttpContext h, Guid id, CombatCheck body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.check", async (season, member, operation) =>
        {
            ValidateDestination(body.Destination);
            var roll = Roll(await SheetAsync(id, member.CharacterId, false, ct), body.Ability);
            operation.Summary = $"{roll.Ability} check: {roll.Die} + {roll.Modifier} = {roll.Total}.";
            return roll;
        }, ct);

    private Task<IResult> AttackAsync(HttpContext h, Guid id, CombatAttack body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.attack", async (season, member, operation) =>
        {
            ValidateDestination(body.Destination);
            ApiFault.Require(!member.CombatRestricted, 403, "combat_restricted", "You do not have permission to attack.");
            ApiFault.Require(member.CharacterId != body.TargetId, 422, "invalid_target", "Select another character.");
            await seasons.TargetAsync(id, body.TargetId, true, ct);
            var attacker = await SheetAsync(id, member.CharacterId, false, ct);
            ApiFault.Require(attacker.HitPoints > 0, 409, "attacker_downed", "A Downed character cannot attack.");
            var weapon = await GearAsync(attacker, attacker.WeaponId, "weapon", ct);
            ApiFault.Require(weapon != null, 409, "weapon_required", "Equip an available weapon first.");
            var target = await SheetAsync(id, body.TargetId, false, ct);
            ApiFault.Require(target.HitPoints > 0, 409, "target_downed", "That character is already Downed.");
            var ac = ArmorClass(target, await GearAsync(target, target.ArmorId, "armor", ct));
            var roll = Roll(attacker, weapon!.Gear.Ability!, CombatRules.WeaponProficiency);
            var hit = CombatRules.Hits(roll.Die, roll.Total, ac);
            var critical = roll.Die == 20;
            var damage = hit ? CombatRules.Damage(weapon.Gear.Value, roll.Modifier, critical) : 0;
            if (hit)
            {
                if (db.Entry(target).State == EntityState.Detached) db.CombatSheets.Add(target);
                target.HitPoints = Math.Max(0, target.HitPoints - damage);
                target.Version++;
            }
            var names = await db.Characters.Where(c => c.Id == member.CharacterId || c.Id == body.TargetId)
                .ToDictionaryAsync(c => c.Id, c => c.DisplayName, ct);
            operation.SubjectCharacterId = body.TargetId;
            operation.Summary = hit ? $"Hit {names[body.TargetId]} with {weapon.Name} for {damage} damage." : $"Missed {names[body.TargetId]} with {weapon.Name}.";
            return new CombatHit(names[member.CharacterId], names[body.TargetId], weapon.Name, roll, ac, hit, critical,
                damage, target.HitPoints, target.HitPoints == 0);
        }, ct);

    private Task<IResult> HealthAsync(HttpContext h, Guid id, Guid character, CombatHealthEdit body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.health", async (season, member, operation) =>
        {
            SeasonAccess.Action(season, member, "season.moderate");
            ApiFault.Require(body.HitPoints is >= 0 and <= CombatRules.MaxHitPoints, 422, "invalid_health", "Choose a valid HP total.");
            var target = await seasons.TargetAsync(id, character, true, ct);
            var sheet = await SheetAsync(id, character, true, ct);
            sheet.HitPoints = body.HitPoints;
            sheet.Version++;
            operation.SubjectCharacterId = character;
            operation.Summary = $"Set HP to {body.HitPoints}.";
            return await ViewAsync(season, target, sheet, ct);
        }, ct);

    private static ResetPotionProduct Potion(CommunityRow season, CombatSheetRow sheet) =>
        new(season.ResetPotionVersion, season.ClassResetPrice, season.ResetPotionLimit, sheet.ResetPurchases, sheet.ResetPotions);

    private async Task<IResult> ReadPotionAsync(HttpContext h, Guid id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, _) = await seasons.RequireAsync(id, actor, ct);
        return Results.Json(Potion(season, await SheetAsync(id, actor, false, ct)));
    }

    private Task<IResult> SavePotionAsync(HttpContext h, Guid id, ResetPotionSettings body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.reset_potion.settings", async (season, member, operation) =>
        {
            SeasonAccess.Owner(season, member.CharacterId);
            ApiFault.Require(body.Version == season.ResetPotionVersion, 409, "reset_product_changed", "Refresh the reset potion settings.");
            ApiFault.Require(body.Price > 0 && body.PerCharacterLimit is >= 0 and <= 1000, 422,
                "invalid_reset_product", "Choose a positive price and a purchase limit between 0 and 1000.");
            if (season.ClassResetPrice != body.Price) season.ClassPolicyVersion++;
            season.ClassResetPrice = body.Price;
            season.ResetPotionLimit = body.PerCharacterLimit;
            season.ResetPotionVersion++;
            operation.Summary = "Updated the Character Reset Potion listing.";
            return Potion(season, await SheetAsync(id, member.CharacterId, false, ct));
        }, ct);

    private Task<IResult> BuyPotionAsync(HttpContext h, Guid id, ResetPotionBuy body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.reset_potion.buy", async (season, member, operation) =>
        {
            ApiFault.Require(body.Version == season.ResetPotionVersion, 409, "reset_product_changed", "Review the current price and limit.");
            var sheet = await SheetAsync(id, member.CharacterId, true, ct);
            ApiFault.Require((season.ResetPotionLimit == 0 || sheet.ResetPurchases < season.ResetPotionLimit)
                && sheet.ResetPurchases < int.MaxValue, 409, "reset_purchase_limit", "You reached your season purchase limit.");
            CurrencyAccounts.Apply(member, -season.ClassResetPrice);
            ShopService.CreditSeason(season, season.ClassResetPrice);
            sheet.ResetPurchases++;
            sheet.ResetPotions++;
            sheet.Version++;
            operation.Summary = "Purchased a Character Reset Potion.";
            return Potion(season, sheet);
        }, ct);

    private Task<IResult> UsePotionAsync(HttpContext h, Guid id, CancellationToken ct) =>
        CommandAsync(h, id, null, "combat.reset", async (season, member, operation) =>
        {
            var sheet = await SheetAsync(id, member.CharacterId, true, ct);
            ApiFault.Require(sheet.ResetPotions > 0, 409, "reset_potion_required", "You need a Character Reset Potion from the Shop.");
            ApiFault.Require(sheet.Allocated.Any(x => x != 0) || member.ClassIds.Length > 0, 409, "reset_empty", "Your character has no stat allocations or classes to reset.");
            sheet.ResetPotions--;
            sheet.Allocated = new int[6];
            sheet.Version++;
            member.ClassIds = [];
            member.ClassCapabilities = [];
            member.ClassSelectionVersion++;
            operation.Summary = "Used a Character Reset Potion to clear stat allocations and selected classes.";
            return await ViewAsync(season, member, sheet, ct);
        }, ct);

    private async Task<IResult> CommandAsync<T>(HttpContext h, Guid seasonId, object? body, string kind,
        Func<CommunityRow, MembershipRow, OperationRow, Task<T>> apply, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var scope = "character:" + actor;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [scope, "season:" + seasonId], ct);
        var (season, member) = await seasons.RequireAsync(seasonId, actor, ct);
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var op = commands.New(h, scope, body, actor, seasonId, kind);
        op.SubjectCharacterId = actor;
        var value = await apply(season, member, op);
        var result = await commands.SaveAsync(h, op, new CommandResult<T>(op.Id, value), ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
