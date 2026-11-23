using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.Shop;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Seasons;

public sealed class SeasonClassService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons, CommandStore commands)
{
    public static readonly string[] GameplayGrants = ["items.potion.create", "items.cooking.create", CraftingTypes.GatherPermission];

    private static ClassPolicy Policy(CommunityRow season) => new(season.ClassSlots, season.ClassResetPrice,
        JsonSerializer.Deserialize<SeasonClass[]>(season.ClassCatalog) ?? [], season.GroupRecipeLimit);

    private static bool NeedsCorrection(ClassPolicy policy, MembershipRow member) =>
        member.ClassIds.Length > policy.Slots || member.ClassIds.Except(policy.Classes.Select(c => c.Id)).Any();

    private static ClassSelection View(CommunityRow season, MembershipRow member)
    {
        var policy = Policy(season);
        return new(policy, season.ClassPolicyVersion, member.ClassIds, member.ClassSelectionVersion, NeedsCorrection(policy, member));
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
        ApiFault.Require(policy != null && policy.Slots is >= 1 and <= 8 && policy.ResetPrice > 0 &&
            policy.GroupRecipeLimit is >= 0 and <= 100 && policy.Classes is { Length: <= 32 }, 422, "invalid_class_policy", "Choose valid class slots, potion price and limits.");
        var seen = new HashSet<Guid>();
        foreach (var c in policy!.Classes)
            ApiFault.Require(c != null && c.Id != Guid.Empty && seen.Add(c.Id) &&
                PlainText.Error(c.Name, 80) == null && PlainText.Error(c.Description, 300, multiline: true, required: false) == null &&
                c.Grants != null && c.Grants.Distinct().Count() == c.Grants.Length && c.Grants.All(GameplayGrants.Contains),
                422, "invalid_class", "Classes require a unique ID, a name and valid gameplay grants.");

        season.ClassSlots = policy.Slots;
        season.ClassResetPrice = policy.ResetPrice;
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
        var price = selected.Intersect(retained).Count() < Math.Min(policy.Slots, retained.Length) ? policy.ResetPrice : 0;
        ApiFault.Require(edit.ConfirmedPrice == price, 409, "class_price_changed", "Review the reset potion cost before confirming.");
        if (price > 0)
        {
            CurrencyAccounts.Apply(member, -price);
            ShopService.CreditSeason(season, price);
        }
        member.ClassIds = selected;
        member.ClassSelectionVersion++;
        ApplyGrants(policy, member);
        season.InventoryVersion++;
        var op = commands.New(h, scope, edit, actor, id, price > 0 ? "classes.reset" : "classes.select");
        op.SubjectCharacterId = actor;
        op.Summary = price > 0 ? $"Used a class reset potion for {price} coins." : "Updated class selection.";
        var result = await commands.SaveAsync(h, op, new CommandResult<ClassSelection>(op.Id, View(season, member)), ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
