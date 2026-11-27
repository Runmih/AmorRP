using System.Security.Cryptography;
using System.Text.Json;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Combat;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Combat;

public sealed partial class CombatService
{
    internal static SkillLibrary Library(CommunityRow season) =>
        new(season.SkillLibraryVersion, JsonSerializer.Deserialize<CombatSkill[]>(season.SkillCatalog) ?? []);

    private static CombatSkill[] GrantedSkills(CommunityRow season, MembershipRow member, SkillLibrary library)
    {
        if (member.ClassIds.Length == 0) return [];
        var ids = SeasonClassService.Selected(season, member).SelectMany(c => c.SkillIds ?? []).ToHashSet();
        return library.Skills.Where(skill => ids.Contains(skill.Id)).ToArray();
    }

    private async Task<IResult> ReadSkillLibraryAsync(HttpContext h, Guid id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, _) = await seasons.RequireAsync(id, actor, ct);
        SeasonAccess.Owner(season, actor);
        return Results.Json(Library(season));
    }

    private Task<IResult> SaveSkillLibraryAsync(HttpContext h, Guid id, SkillLibrary body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.skills.configure", async (season, member, operation) =>
        {
            SeasonAccess.Owner(season, member.CharacterId);
            ApiFault.Require(body.Version == season.SkillLibraryVersion, 409, "skill_library_changed", "Reload the skill library.");
            var ids = new HashSet<Guid>();
            ApiFault.Require(body.Skills is { Length: <= 64 } && body.Skills.All(skill =>
                ValidSkill(skill) && ids.Add(skill.Id)), 422, "invalid_skill", "Choose valid names, targets, rolls, and dice for each ability.");
            ApiFault.Require(SeasonClassService.Classes(season).SelectMany(c => c.SkillIds ?? []).All(ids.Contains),
                409, "skill_in_use", "Remove the ability from its classes before deleting it.");
            foreach (var reference in body.Skills!.Select(skill => skill.StatusEffect).OfType<StatusEffectReference>().Distinct())
                await effects.FindAsync(id, reference, ct);
            season.SkillCatalog = JsonSerializer.Serialize(body.Skills);
            season.SkillLibraryVersion++;
            operation.Summary = "Updated the skill and spell library.";
            return Library(season);
        }, ct);

    private static bool ValidSkill(CombatSkill? skill) => skill != null && skill.Id != Guid.Empty
        && PlainText.Error(skill.Name, 80) == null && PlainText.Error(skill.Description, 300, multiline: true, required: false) == null
        && skill.Kind is "skill" or "spell"
        && skill.Icon is "strike" or "magic" or "heal" or "shield" or "spark"
        && skill.Target is "self" or "selected"
        && skill.Resolution is "automatic" or "attack" or "save"
        && CombatRules.Abilities.Contains(skill.RollAbility)
        && skill.SaveDc is >= 1 and <= 30
        && (skill.Effect == "status"
            ? skill.StatusEffect != null && skill.DiceCount == 0 && skill.DiceSides == 0 && skill.EffectAbility == null
            : skill.Effect is "damage" or "healing" && skill.DiceCount is >= 1 and <= 20 && skill.DiceSides is >= 1 and <= 200
                && (skill.EffectAbility == null || CombatRules.Abilities.Contains(skill.EffectAbility)));

    private Task<IResult> UseSkillAsync(HttpContext h, Guid id, SkillUse body, CancellationToken ct) =>
        CommandAsync(h, id, body, "combat.skill", async (season, member, operation) =>
        {
            ValidateDestination(body.Destination);
            ApiFault.Require(season.SeasonState == "active", 409, "season_inactive", "An active season is required.");
            ApiFault.Require(!member.CombatRestricted, 403, "combat_restricted", "You do not have permission to use combat actions.");
            var library = Library(season);
            ApiFault.Require(body.LibraryVersion == library.Version, 409, "skill_library_changed", "Review the updated ability first.");
            var skill = GrantedSkills(season, member, library).FirstOrDefault(item => item.Id == body.SkillId);
            ApiFault.Require(skill != null, 403, "skill_not_granted", "Your selected classes do not grant this ability.");
            var actor = await SheetAsync(id, member.CharacterId, false, ct);
            ApiFault.Require(actor.HitPoints > 0, 409, "attacker_downed", "A Downed character cannot use combat actions.");
            ApiFault.Require(skill!.Target == "self" ? body.TargetId == null : body.TargetId != null && body.TargetId != member.CharacterId,
                422, "invalid_target", "Choose the target supported by this ability.");
            var targetMember = body.TargetId is { } other ? await seasons.TargetAsync(id, other, true, ct) : member;
            var target = targetMember == member ? actor : await SheetAsync(id, targetMember.CharacterId, false, ct);
            ApiFault.Require(skill.Effect != "damage" || target.HitPoints > 0, 409, "target_downed", "That character is already Downed.");

            CombatRoll? roll = null;
            int? difficulty = null;
            var applied = true;
            var critical = false;
            if (skill.Resolution == "attack")
            {
                var status = await effects.ModifierAsync(id, member.CharacterId, "attack", ct);
                roll = Roll(actor, skill.RollAbility, CombatRules.WeaponProficiency, status);
                difficulty = await ArmorClassAsync(target, await GearAsync(target, target.ArmorId, "armor", ct), ct);
                applied = CombatRules.Hits(roll.Die, roll.Total, difficulty.Value);
                critical = roll.Die == 20;
            }
            if (skill.Resolution == "save")
            {
                roll = Roll(target, skill.RollAbility);
                difficulty = skill.SaveDc;
                applied = roll.Total < difficulty.Value;
            }

            var rolled = applied ? RollDice(CombatRules.DamageDiceCount(skill.DiceCount, critical), skill.DiceSides) : 0;
            var modifier = skill.EffectAbility is { } ability ? Modifier(actor, ability) : 0;
            var points = applied ? CombatRules.Damage(rolled, modifier) : 0;
            var before = target.HitPoints;
            var stayedStanding = false;
            if (points > 0 && skill.Effect == "damage") stayedStanding = ApplyDamage(season, targetMember, target, points);
            if (points > 0 && skill.Effect == "healing") SetHitPoints(target, Math.Min(MaximumHp(target), target.HitPoints + points));

            var statusEffect = applied ? await effects.FindAsync(id, skill.StatusEffect, ct) : null;
            var appliedStatus = statusEffect != null && await effects.ApplyAsync(id, targetMember.CharacterId, statusEffect, ct)
                ? statusEffect.Name : null;
            var names = await db.Characters.Where(c => c.Id == member.CharacterId || c.Id == targetMember.CharacterId)
                .ToDictionaryAsync(c => c.Id, c => c.DisplayName, ct);
            operation.SubjectCharacterId = targetMember.CharacterId;
            var actual = Math.Abs(before - target.HitPoints);
            var outcome = skill.Effect == "status" ? appliedStatus ?? "no effect" : applied ? $"{actual} {skill.Effect}" : "no effect";
            operation.Summary = $"Used {skill.Name} on {names[targetMember.CharacterId]}: {outcome}.";
            return new SkillResult(skill.Name, names[member.CharacterId], names[targetMember.CharacterId], skill.Effect,
                applied, roll, difficulty, rolled, modifier, actual, target.HitPoints, stayedStanding, appliedStatus);
        }, ct);

    private bool ApplyDamage(CommunityRow season, MembershipRow member, CombatSheetRow sheet, int damage)
    {
        var hp = Math.Max(0, sheet.HitPoints - damage);
        var period = SeasonAccess.Period(time.GetUtcNow());
        var stayStanding = hp == 0 && sheet.HitPoints > 0 && sheet.StayedStandingPeriod != period
            && SeasonClassService.Selected(season, member).Any(c => c.DownedBehavior == "stay_standing");
        if (stayStanding)
        {
            sheet.StayedStandingPeriod = period;
            if (sheet.HitPoints == 1) sheet.Version++;
        }
        SetHitPoints(sheet, stayStanding ? 1 : hp);
        return stayStanding;
    }

    private void SetHitPoints(CombatSheetRow sheet, int hp)
    {
        if (hp == sheet.HitPoints) return;
        if (db.Entry(sheet).State == EntityState.Detached) db.CombatSheets.Add(sheet);
        sheet.HitPoints = hp;
        sheet.Version++;
    }

    private static int RollDice(int count, int sides)
    {
        var total = 0;
        for (var i = 0; i < count; i++) total += RandomNumberGenerator.GetInt32(1, sides + 1);
        return total;
    }
}
