using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Core.Combat;
using AmorRP.Server.Features.Combat;
using AmorRP.Contracts.Seasons;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Seasons;

public sealed class SeasonClassService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons, CommandStore commands)
{
    internal static SeasonClass[] Classes(CommunityRow season) =>
        JsonSerializer.Deserialize<SeasonClass[]>(season.ClassCatalog) ?? [];

    internal static IEnumerable<SeasonClass> Selected(CommunityRow season, MembershipRow member) =>
        member.ClassIds.Length == 0 ? [] : Classes(season).Where(c => member.ClassIds.Contains(c.Id));

    internal static int WeeklyBonus(CommunityRow season, MembershipRow member, string kind)
    {
        if (member.ClassIds.Length == 0) return 0;
        var grant = kind switch
        {
            "potion_points" => "items.potion.create",
            "cooking_points" => "items.cooking.create",
            "gathering_points" => CraftingTypes.GatherPermission,
            CombatRules.BlacksmithingQuota => CraftingTypes.BlacksmithPermission,
            _ => null
        };
        if (grant == null || !SeasonAccess.HasAction(season, member, grant)) return 0;
        return Selected(season, member).Where(c => c.Grants.Contains(grant))
            .Select(c => c.Crafting is { } bonus ? kind switch
            {
                "potion_points" => bonus.Alchemy,
                "cooking_points" => bonus.Cooking,
                "gathering_points" => bonus.Gathering,
                _ => bonus.Blacksmithing
            } : 0).DefaultIfEmpty().Max();
    }

    private static bool ValidBonus(int bonus, SeasonClass c, int grantIndex) =>
        bonus is >= 0 and <= 100000 && (bonus == 0 || c.Grants.Contains(CraftingTypes.ClassGameplayGrants[grantIndex]));

    private static ClassPolicy Policy(CommunityRow season) =>
        new(season.ClassSlots, season.ClassResetPrice, Classes(season), season.GroupRecipeLimit);

    private static bool NeedsCorrection(ClassPolicy policy, MembershipRow member) =>
        member.ClassIds.Length > policy.Slots || member.ClassIds.Except(policy.Classes.Select(c => c.Id)).Any();

    private static ClassSelection View(CommunityRow season, MembershipRow member)
    {
        var policy = Policy(season);
        var publishedIds = policy.Classes.SelectMany(c => c.SkillIds ?? []).ToHashSet();
        var abilities = CombatService.Library(season).Skills.Where(skill => publishedIds.Contains(skill.Id)).ToArray();
        return new(policy, season.ClassPolicyVersion, member.ClassIds, member.ClassSelectionVersion, NeedsCorrection(policy, member), abilities);
    }

    private static void ApplyGrants(ClassPolicy policy, MembershipRow member) =>
        member.ClassCapabilities = policy.Classes.Where(c => member.ClassIds.Contains(c.Id)).SelectMany(c => c.Grants).Distinct().ToArray();

    public async Task<IResult> ReadAsync(HttpContext h, Guid id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, member) = await seasons.RequireAsync(id, actor, ct);
        return Results.Json(View(season, member));
    }

    public async Task<IResult> PolicyAsync(HttpContext h, Guid id, ClassPolicyEdit edit, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var scope = "character:" + actor;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [scope, "season:" + id], ct);
        var (season, member) = await seasons.RequireAsync(id, actor, ct);
        SeasonAccess.Owner(season, actor);
        var replay = await commands.ReplayAsync(h, scope, edit, ct);
        if (replay != null) return commands.Replay(replay);
        ApiFault.Require(edit.ExpectedVersion == season.ClassPolicyVersion, 409, "class_policy_changed", "Reload the class settings.");
        var policy = edit.Policy;
        ApiFault.Require(policy != null && policy.Slots is >= 1 and <= 8 &&
            policy.GroupRecipeLimit is >= 0 and <= 100 && policy.Classes is { Length: <= 32 }, 422, "invalid_class_policy", "Choose valid class slots and recipe limits.");
        var seen = new HashSet<Guid>();
        var skillIds = CombatService.Library(season).Skills.Select(skill => skill.Id).ToHashSet();
        foreach (var c in policy!.Classes)
            ApiFault.Require(c != null && c.Id != Guid.Empty && seen.Add(c.Id) &&
                PlainText.Error(c.Name, 80) == null && PlainText.Error(c.Description, 300, multiline: true, required: false) == null &&
                c.Grants != null && c.Grants.Distinct().Count() == c.Grants.Length && c.Grants.All(CraftingTypes.ClassGameplayGrants.Contains)
                && (c.SkillIds == null || (c.SkillIds.Length <= 16 && c.SkillIds.Distinct().Count() == c.SkillIds.Length && c.SkillIds.All(skillIds.Contains)))
                && c.DownedBehavior is "normal" or "stay_standing"
                && (c.Crafting is not { } bonus || (ValidBonus(bonus.Alchemy, c, 0) && ValidBonus(bonus.Cooking, c, 1)
                    && ValidBonus(bonus.Gathering, c, 2) && ValidBonus(bonus.Blacksmithing, c, 3))),
                422, "invalid_class", "Choose valid class grants, crafting bonuses, skills and Downed behavior.");

        season.ClassSlots = policy.Slots;
        season.GroupRecipeLimit = policy.GroupRecipeLimit;
        season.ClassCatalog = JsonSerializer.Serialize(policy.Classes);
        season.ClassPolicyVersion++;
        season.InventoryVersion++;
        var members = await db.Memberships.Where(m => m.CommunityId == id && m.ClassIds.Length > 0).ToArrayAsync(ct);
        foreach (var chosen in members) ApplyGrants(policy, chosen);
        var op = commands.New(h, scope, edit, actor, id, "classes.configure");
        op.Summary = "Updated classes and recipe limits.";
        var result = await commands.SaveAsync(h, op, new CommandResult<ClassSelection>(op.Id, View(season, member)), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IResult> SelectAsync(HttpContext h, Guid id, ClassSelectionEdit edit, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var scope = "character:" + actor;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [scope, "season:" + id], ct);
        var (season, member) = await seasons.RequireAsync(id, actor, ct);
        var replay = await commands.ReplayAsync(h, scope, edit, ct);
        if (replay != null) return commands.Replay(replay);
        ApiFault.Require(edit.PolicyVersion == season.ClassPolicyVersion && edit.SelectionVersion == member.ClassSelectionVersion,
            409, "class_selection_changed", "Reload the available classes and your current selection.");
        var policy = Policy(season);
        ApiFault.Require(edit.Selected != null && edit.Selected.Length <= policy.Slots &&
            edit.Selected.Distinct().Count() == edit.Selected.Length && edit.Selected.All(i => policy.Classes.Any(c => c.Id == i)),
            422, "invalid_class_selection", "Choose distinct available classes within your slot limit.");
        var selected = edit.Selected!;
        ApiFault.Require(!member.ClassIds.ToHashSet().SetEquals(selected), 422, "class_selection_unchanged", "Choose a different set of classes.");
        // An owner policy change permits the smallest correction needed to fit the new choices.
        var retained = member.ClassIds.Intersect(policy.Classes.Select(c => c.Id)).ToArray();
        ApiFault.Require(selected.Intersect(retained).Count() >= Math.Min(policy.Slots, retained.Length), 409,
            "reset_potion_required", "Use a Character Reset Potion before replacing chosen classes.");
        member.ClassIds = selected;
        member.ClassSelectionVersion++;
        ApplyGrants(policy, member);
        season.InventoryVersion++;
        var op = commands.New(h, scope, edit, actor, id, "classes.select");
        op.SubjectCharacterId = actor;
        op.Summary = "Updated class selection.";
        var result = await commands.SaveAsync(h, op, new CommandResult<ClassSelection>(op.Id, View(season, member)), ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
