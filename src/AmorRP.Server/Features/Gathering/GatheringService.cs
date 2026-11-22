using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Catalog;
using AmorRP.Server.Features.Inventory;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Gathering;

public sealed class GatheringService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons,
    CommandStore commands, InventoryService inventory, InventoryDelivery delivery, CatalogService catalog, TimeProvider time, IOptions<ServicePolicyOptions> limits)
{
    public const string QuotaKind = "gathering_points";

    public async Task<IResult> ReadAsync(HttpContext h, Guid seasonId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, _) = await seasons.RequireAsync(seasonId, actor, ct);
        return Results.Json(await ViewAsync(season, ct));
    }

    public Task<IResult> WriteAsync(HttpContext h, Guid seasonId, GatheringSettingsEdit body, CancellationToken ct) =>
        RunAsync(h, seasonId, body, "gathering.policy", async (season, member, op) =>
        {
            SeasonAccess.Owner(season, member.CharacterId);
            var max = limits.Value.MaxWeeklyPotionPoints;
            ApiFault.Require(season.GatheringVersion == body.ExpectedVersion, 409, "gathering_policy_changed", "Gathering settings changed. Reload them before saving.");
            ApiFault.Require(body.WeeklyPoints >= 0 && body.WeeklyPoints <= max
                && new[] { body.AlchemyCost, body.AlchemyBonus, body.CookingCost, body.CookingBonus }.All(x => x > 0 && x <= max),
                422, "invalid_gathering_policy", "Use a valid budget and positive costs and bonuses.");
            SeasonAccess.PromotePolicy(season, time.GetUtcNow());
            if (season.GatheringVersion == 0) season.CurrentGatheringPoints = body.WeeklyPoints;
            season.NextGatheringPoints = body.WeeklyPoints;
            season.GatheringVersion++;
            var alchemy = await SavePackageAsync(season.Id, CraftingTypes.AlchemySupplies, "Alchemy Supplies", body.AlchemyCost, body.AlchemyBonus, ct);
            var cooking = await SavePackageAsync(season.Id, CraftingTypes.CookingSupplies, "Cooking Supplies", body.CookingCost, body.CookingBonus, ct);
            season.InventoryVersion++;
            return View(season, [alchemy, cooking], time.GetUtcNow());
        }, ct);

    public Task<IResult> GatherAsync(HttpContext h, Guid seasonId, GatherSupplies body, CancellationToken ct) =>
        RunAsync(h, seasonId, body, "gathering.create", async (season, member, op) =>
        {
            SeasonAccess.Action(season, member, CraftingTypes.GatherPermission);
            ApiFault.Require(CraftingTypes.IsSupply(body.TypeId) && body.Quantity > 0 && body.Quantity <= limits.Value.MaxCreationQuantity,
                422, "invalid_supply", "Choose a supply package and valid quantity.");
            var definition = await db.Definitions.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.TypeId == body.TypeId && !x.Retired, ct);
            ApiFault.Require(definition != null, 404, "not_found", "Supply packages are not configured.");
            ApiFault.Require(definition!.Revision == body.ExpectedRevision, 409, "definition_changed", "Review the current package cost and bonus.");
            var revision = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == seasonId && x.DefinitionId == definition.Id && x.Revision == definition.Revision, ct);
            await catalog.RequireCategoryAsync(seasonId, revision.CategoryId, definition.TypeId, ct);
            var cost = (long)revision.CreationCost * body.Quantity;
            ApiFault.Require(cost <= int.MaxValue, 422, "invalid_quantity", "The total package cost is too large.");
            var quota = await inventory.SpendAsync(season, member.CharacterId, QuotaKind, (int)cost, ct);
            var holding = await delivery.AddAsync(seasonId, member.CharacterId, definition, revision, body.Quantity, ct);
            season.InventoryVersion++;
            op.SubjectCharacterId = member.CharacterId; op.Summary = $"Gathered {body.Quantity} × {revision.Name}.";
            return new ProductionResult(InventoryService.View(holding), InventoryService.ViewQuota(quota));
        }, ct);

    public Task<IResult> UseAsync(HttpContext h, Guid seasonId, Guid holdingId, UseSupplies body, CancellationToken ct) =>
        RunAsync(h, seasonId, body, "gathering.use", async (season, member, op) =>
        {
            var holding = await db.Holdings.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.Id == holdingId
                && x.CharacterId == member.CharacterId && x.Quantity > 0, ct);
            ApiFault.Require(holding != null && CraftingTypes.IsSupply(holding.TypeId), 404, "not_found", "Supply package unavailable.");
            ApiFault.Require(holding!.Version == body.ExpectedHoldingVersion && holding.Quantity > holding.Reserved,
                409, "holding_changed", "Refresh the package and check that it is not reserved for a trade.");
            var craftType = holding.TypeId == CraftingTypes.AlchemySupplies ? "potion" : "meal";
            SeasonAccess.Action(season, member, CraftingTypes.CraftPermission(craftType));
            var revision = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == seasonId && x.DefinitionId == holding.DefinitionId
                && x.Revision == holding.DefinitionRevision, ct);
            var quota = await inventory.QuotaAsync(season, member.CharacterId, CraftingTypes.QuotaKind(craftType), ct);
            ApiFault.Require(revision.BonusPoints > 0 && (long)quota.Limit + quota.Bonus + revision.BonusPoints <= int.MaxValue,
                409, "quota_overflow", "The points would exceed the supported numeric range. The package is still available.");
            if (db.Entry(quota).State == EntityState.Detached) db.Quotas.Add(quota);
            quota.Bonus += revision.BonusPoints;
            holding.Quantity--; holding.Version++; season.InventoryVersion++;
            op.SubjectCharacterId = member.CharacterId;
            op.Summary = $"Used {revision.Name} for {revision.BonusPoints} crafting points.";
            return new ProductionResult(InventoryService.View(holding), InventoryService.ViewQuota(quota));
        }, ct);

    private async Task<SupplyPackage> SavePackageAsync(Guid season, string type, string name, int cost, int bonus, CancellationToken ct)
    {
        var definition = await db.Definitions.SingleOrDefaultAsync(x => x.CommunityId == season && x.TypeId == type, ct);
        Guid category;
        if (definition == null)
        {
            var row = new CategoryRow { CommunityId = season, Name = name, AllowedTypeIds = [type] };
            db.Categories.Add(row);
            category = row.Id;
            definition = new DefinitionRow { CommunityId = season, TypeId = type };
            db.Definitions.Add(definition);
        }
        else
        {
            var current = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == season && x.DefinitionId == definition.Id && x.Revision == definition.Revision, ct);
            if (current.CreationCost == cost && current.BonusPoints == bonus) return new(type, name, definition.Revision, cost, bonus);
            category = current.CategoryId;
            definition.Revision++;
        }
        db.DefinitionRevisions.Add(new DefinitionRevisionRow { CommunityId = season, DefinitionId = definition.Id, Revision = definition.Revision,
            Name = name, CategoryId = category, Description = $"Adds {bonus} {(type == CraftingTypes.AlchemySupplies ? "Alchemy" : "Cooking")} points until the weekly reset.",
            Enabled = true, CreationCost = cost, BonusPoints = bonus });
        return new(type, name, definition.Revision, cost, bonus);
    }

    private async Task<GatheringSettings> ViewAsync(CommunityRow season, CancellationToken ct)
    {
        var rows = await (from d in db.Definitions.AsNoTracking()
            join r in db.DefinitionRevisions on new { d.CommunityId, DefinitionId = d.Id, d.Revision } equals new { r.CommunityId, r.DefinitionId, r.Revision }
            where d.CommunityId == season.Id && (d.TypeId == CraftingTypes.AlchemySupplies || d.TypeId == CraftingTypes.CookingSupplies)
            orderby d.TypeId select new SupplyPackage(d.TypeId, r.Name, r.Revision, r.CreationCost, r.BonusPoints)).ToArrayAsync(ct);
        return View(season, rows, time.GetUtcNow());
    }

    private static GatheringSettings View(CommunityRow season, SupplyPackage[] rows, DateTimeOffset now) =>
        new(season.GatheringVersion, SeasonAccess.GatheringPoints(season, now), season.NextGatheringPoints, SeasonAccess.Period(now).AddDays(7), rows);

    private async Task<IResult> RunAsync<T>(HttpContext h, Guid seasonId, object body, string kind,
        Func<CommunityRow, MembershipRow, OperationRow, Task<T>> action, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var scope = "character:" + actor;
        await CommandStore.LockAsync(db, [scope, "season:" + seasonId], ct);
        var (season, member) = await seasons.RequireAsync(seasonId, actor, ct);
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var op = commands.New(h, scope, body, actor, seasonId, kind);
        var result = await action(season, member, op);
        var response = await commands.SaveAsync(h, op, new CommandResult<T>(op.Id, result), ct);
        await tx.CommitAsync(ct);
        return response;
    }
}
