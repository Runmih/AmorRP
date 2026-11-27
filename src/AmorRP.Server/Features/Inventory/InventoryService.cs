using AmorRP.Contracts.Common;
using AmorRP.Core.Combat;
using AmorRP.Server.Features.Combat;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Catalog;
using AmorRP.Server.Features.Gathering;
using AmorRP.Server.Features.Moodles;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Inventory;

public sealed partial class InventoryService(AmorDbContext db, SessionAccess sessions, SeasonAccess communities,
    CatalogService catalog, InventoryDelivery delivery, StatusEffectService effects, CombatService combat, CommandStore commands, PageCursor cursors, TimeProvider time, IOptions<ServicePolicyOptions> policy)
{
    public static Holding View(HoldingRow x) => new(x.Id, x.CommunityId, x.CharacterId, x.TypeId, x.Name, x.CategoryId,
        x.Quantity, x.Reserved, x.Quantity - x.Reserved, x.Version, x.DefinitionId, x.DefinitionRevision, x.LetterId);
    internal static int EffectiveQuotaLimit(CommunityRow season, MembershipRow member, QuotaRow quota) =>
        quota.Limit + SeasonClassService.WeeklyBonus(season, member, quota.Kind);

    internal static Quota ViewQuota(CommunityRow season, MembershipRow member, QuotaRow quota)
    {
        var limit = EffectiveQuotaLimit(season, member, quota);
        var remaining = (int)Math.Clamp((long)limit + quota.Bonus - quota.Used, 0, int.MaxValue);
        return new(quota.Kind, quota.PeriodStart, quota.PeriodStart.AddDays(7), limit, quota.Used, remaining, quota.Bonus);
    }
    internal async Task<QuotaRow> QuotaAsync(CommunityRow community, Guid actor, string kind, CancellationToken ct)
    {
        var period = SeasonAccess.Period(time.GetUtcNow());
        return await db.Quotas.SingleOrDefaultAsync(x => x.CommunityId == community.Id && x.CharacterId == actor && x.Kind == kind && x.PeriodStart == period, ct)
            ?? new QuotaRow { CommunityId = community.Id, CharacterId = actor, Kind = kind, PeriodStart = period,
                Limit = kind == CombatRules.BlacksmithingQuota ? community.BlacksmithingPoints : kind == GatheringService.QuotaKind ? SeasonAccess.GatheringPoints(community, time.GetUtcNow()) : kind == "cooking_points" ? SeasonAccess.Policies(community, time.GetUtcNow()).Current.CookingPoints : kind == "potion_points" ? SeasonAccess.Policies(community, time.GetUtcNow()).Current.PotionPoints : SeasonAccess.Policies(community, time.GetUtcNow()).Current.Letters };
    }
    internal async Task<Quota> SpendAsync(CommunityRow community, MembershipRow member, string kind, int cost, CancellationToken ct)
    {
        var quota = await QuotaAsync(community, member.CharacterId, kind, ct);
        var before = ViewQuota(community, member, quota);
        ApiFault.Require(cost > 0 && cost <= before.Remaining && cost <= int.MaxValue - quota.Used,
            409, "quota_exhausted", "Weekly allowance is insufficient. No items were created.");
        if (db.Entry(quota).State == EntityState.Detached) db.Quotas.Add(quota);
        quota.Used += cost;
        return ViewQuota(community, member, quota);
    }
    private async Task CapacityAsync(Guid communityId, Guid actor, CancellationToken ct) => ApiFault.Require(
        await db.Holdings.CountAsync(x => x.CommunityId == communityId && x.CharacterId == actor && x.Quantity > 0, ct) < policy.Value.MaxHoldingsPerCharacterCommunity,
        409, "inventory_full", "Inventory holding limit reached.");
    private async Task<HoldingRow> HoldingAsync(Guid communityId, Guid actor, Guid id, CancellationToken ct)
    {
        var row = await db.Holdings.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.CharacterId == actor && x.Id == id && x.Quantity > 0, ct);
        ApiFault.Require(row != null, 404, "not_found", "Holding not found or no longer owned."); return row!;
    }
    private void Quantity(int n, bool creation = false) => ApiFault.Require(n > 0 && n <= (creation ? policy.Value.MaxCreationQuantity : 1000), 422, "invalid_quantity", "Quantity exceeds supported bounds.");
    public async Task<IResult> QuotasAsync(HttpContext h, Guid communityId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        var quotas = new List<Quota>();
        foreach (var kind in new[] { "potion_points", "letters", "cooking_points" })
            quotas.Add(ViewQuota(community, member, await QuotaAsync(community, actor, kind, ct)));
        if (SeasonAccess.HasAction(community, member, CraftingTypes.BlacksmithPermission))
            quotas.Add(ViewQuota(community, member, await QuotaAsync(community, actor, CombatRules.BlacksmithingQuota, ct)));
        if (SeasonAccess.HasAction(community, member, CraftingTypes.GatherPermission))
            quotas.Add(ViewQuota(community, member, await QuotaAsync(community, actor, GatheringService.QuotaKind, ct)));
        return Results.Json(new Quotas(communityId, actor, quotas.ToArray()));
    }
    public async Task<IResult> ListAsync(HttpContext h, Guid communityId, Guid? target, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        if (target.HasValue && target != actor) { SeasonAccess.Action(community, member, "inventory.remove"); await communities.TargetAsync(communityId, target.Value, community.OwnerCharacterId != actor, ct); }
        var owner = target ?? actor;
        var type = h.Request.Query["typeId"].ToString(); var search = h.Request.Query["search"].ToString();
        var typesText = h.Request.Query["typeIds"].ToString();
        var types = typesText.Length == 0 ? Array.Empty<string>() : typesText.Split(',');
        ApiFault.Require(types.Length <= CraftingTypes.All.Length && types.All(x => CraftingTypes.All.Contains(x)) && (types.Length == 0 || type.Length == 0), 422, "invalid_request", "Invalid type filters.");
        var categoryText = h.Request.Query["categoryId"].ToString(); var sort = h.Request.Query["sort"].ToString();
        ApiFault.Require(search.Length <= 100 && (type == "" || CraftingTypes.All.Contains(type)) && (sort == "" || sort is "name" or "type" or "category"), 422, "invalid_request", "Invalid inventory filters.");
        Guid? category = null;
        if (categoryText.Length > 0) { ApiFault.Require(Guid.TryParse(categoryText, out var parsed), 422, "invalid_request", "Invalid category."); category = parsed; }
        var scope = $"inventory:{communityId}:{actor}:{owner}:{type}:{typesText}:{category}:{search}:{sort}"; var (limit, after) = cursors.Read(h, scope);
        var query = db.Holdings.AsNoTracking().Where(x => x.CommunityId == communityId && x.CharacterId == owner && x.Quantity > 0);
        if (type.Length > 0) query = query.Where(x => x.TypeId == type);
        if (types.Length > 0) query = query.Where(x => types.Contains(x.TypeId));
        if (category.HasValue) query = query.Where(x => x.CategoryId == category);
        if (search.Length > 0) query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));
        var ordered = from holding in query join cat in db.Categories on new { holding.CommunityId, Id = holding.CategoryId } equals new { cat.CommunityId, cat.Id }
                      select new SortRow { Holding = holding, Term = sort == "type" ? holding.TypeId : sort == "category" ? cat.Name : holding.Name };
        if (after.HasValue)
        {
            // An issued cursor references a retained holding; zero quantities remain for pagination/recovery.
            var anchor = await (from x in db.Holdings where x.CommunityId == communityId && x.CharacterId == owner && x.Id == after
                join cat in db.Categories on new { x.CommunityId, Id = x.CategoryId } equals new { cat.CommunityId, cat.Id }
                select new { Term = sort == "type" ? x.TypeId : sort == "category" ? cat.Name : x.Name, x.Id }).SingleOrDefaultAsync(ct);
            ApiFault.Require(anchor != null, 422, "invalid_request", "Inventory cursor expired. Refresh the list.");
            var term = anchor!.Term; var id = anchor.Id;
            ordered = ordered.Where(x => string.Compare(x.Term, term) > 0 || (x.Term == term && x.Holding.Id.CompareTo(id) > 0));
        }
        var rows = await ordered.OrderBy(x => x.Term).ThenBy(x => x.Holding.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new HoldingPage(rows.Take(limit).Select(x => View(x.Holding)).ToArray(), rows.Length > limit ? cursors.Write(rows[limit - 1].Holding.Id, scope) : null, community.InventoryVersion.ToString()));
    }
    private sealed class SortRow { public HoldingRow Holding { get; set; } = null!; public string Term { get; set; } = ""; }
    public async Task<IResult> DetailsAsync(HttpContext h, Guid communityId, Guid id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await communities.RequireAsync(communityId, actor, ct); var holding = await HoldingAsync(communityId, actor, id, ct);
        Definition? definition = null; Letter? letter = null;
        if (CraftingTypes.IsStackable(holding.TypeId))
        {
            var row = await db.Definitions.SingleAsync(x => x.CommunityId == communityId && x.Id == holding.DefinitionId, ct);
            var rev = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == communityId && x.DefinitionId == holding.DefinitionId && x.Revision == holding.DefinitionRevision, ct);
            definition = CatalogService.View(row, rev) with { GroupId = null };
        }
        else letter = await LetterViewAsync(await db.Letters.SingleAsync(x => x.CommunityId == communityId && x.Id == holding.LetterId, ct), holding, ct);
        h.Response.Headers.ETag = CommandStore.ETag("holding", holding.Id, holding.Version);
        return Results.Json(new HoldingDetails(View(holding), definition, letter));
    }
    public async Task<IResult> ProduceAsync(HttpContext h, Guid communityId, object body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        DefinitionRow? productionDefinition = null;
        if (body is PotionCreate requested)
        {
            productionDefinition = await db.Definitions.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == requested.DefinitionId, ct);
            ApiFault.Require(productionDefinition != null, 404, "not_found", "Recipe unavailable.");
            ApiFault.Require(CraftingTypes.IsRecipe(productionDefinition!.TypeId), 422, "invalid_type", "Gather supply packages from Gathering and Hunting.");
            await GroupRecipes.RequireAsync(db, communityId, actor, productionDefinition.GroupId, ct);
            SeasonAccess.Action(community, member, CraftingTypes.CraftPermission(productionDefinition.TypeId));
        }
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        HoldingRow holding; Quota quota;
        if (body is PotionCreate potion)
        {
            Quantity(potion.Quantity, creation: true);
            var row = productionDefinition;
            ApiFault.Require(row != null && !row.Retired, 404, "not_found", "Potion definition unavailable.");
            SeasonAccess.Action(community, member, CraftingTypes.CraftPermission(row!.TypeId));
            ApiFault.Require(row!.Revision == potion.ExpectedDefinitionRevision, 409, "definition_changed", "Potion recipe changed. Review the current cost and message.");
            var rev = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == communityId && x.DefinitionId == row.Id && x.Revision == row.Revision, ct);
            ApiFault.Require(rev.Enabled, 409, "definition_unavailable", "Potion production is disabled."); await catalog.RequireCategoryAsync(communityId, rev.CategoryId, row.TypeId, ct);
            var cost = (long)potion.Quantity * rev.CreationCost; ApiFault.Require(cost <= int.MaxValue, 422, "invalid_quantity", "Production cost exceeds supported bounds.");
            quota = await SpendAsync(community, member, CraftingTypes.QuotaKind(row.TypeId), (int)cost, ct);
            holding = await delivery.AddAsync(communityId, actor, row, rev, potion.Quantity, ct);
        }
        else
        {
            var letter = (LetterCreate)body; ApiFault.Require(letter.TypeId is "book" or "letter" or "note", 422, "invalid_type", "Choose book, letter or note.");
            await catalog.RequireCategoryAsync(communityId, letter.CategoryId, letter.TypeId, ct); ValidateLetter(letter.Title, letter.Body); await CapacityAsync(communityId, actor, ct);
            quota = await SpendAsync(community, member, "letters", 1, ct);
            var row = new LetterRow { CommunityId = communityId, AuthorCharacterId = actor, CategoryId = letter.CategoryId, TypeId = letter.TypeId, Title = letter.Title.Trim(), Body = letter.Body }; db.Letters.Add(row);
            holding = new() { CommunityId = communityId, CharacterId = actor, TypeId = letter.TypeId, Name = row.Title, CategoryId = row.CategoryId, Quantity = 1, LetterId = row.Id }; db.Holdings.Add(holding);
        }
        community.InventoryVersion++;
        var op = commands.New(h, scope, body, actor, communityId, body is PotionCreate ? "potion.create" : "letter.create"); op.SubjectCharacterId = actor;
        op.Summary = body is PotionCreate pc ? $"Created {pc.Quantity} {holding.TypeId} units{(productionDefinition?.GroupId == null ? ": " + holding.Name : " from a group recipe")}." : $"Created {holding.TypeId}: {holding.Name}.";
        var result = await commands.SaveAsync(h, op, new CommandResult<ProductionResult>(op.Id, new(View(holding), quota)), ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid communityId, Guid? id, object body, CancellationToken ct, Guid? targetCharacterId = null)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        var target = targetCharacterId ?? (body is InventoryRemoval removal ? removal.TargetCharacterId : actor);
        var removalReason = body switch { HoldingChange x => x.Reason, HoldingDeletion x => x.Reason, InventoryRemoval x => x.Reason, _ => null };
        if (target != actor) { SeasonAccess.Action(community, member, "inventory.remove"); ApiFault.Text(removalReason, 500); await communities.TargetAsync(communityId, target, true, ct); }
        if (body is InventoryRemoval rm) { SeasonAccess.Action(community, member, "inventory.remove"); ApiFault.Text(rm.Reason, 500); await communities.TargetAsync(communityId, target, true, ct); }
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var holding = await HoldingAsync(communityId, target, id ?? ((InventoryRemoval)body).HoldingId, ct);
        var version = body switch { ConsumptionRequest b => b.ExpectedHoldingVersion, DiscardRequest b => b.ExpectedHoldingVersion, InventoryRemoval b => b.ExpectedHoldingVersion, HoldingChange b => b.ExpectedHoldingVersion, HoldingDeletion b => b.ExpectedHoldingVersion, _ => 0 };
        ApiFault.Require(version == holding.Version, 409, "holding_changed", "Holding changed. Refresh and review its available quantity.");
        var quantity = body switch { DiscardRequest b => b.Quantity, InventoryRemoval b => b.Quantity, HoldingChange b => holding.Quantity - b.RemainingQuantity, HoldingDeletion => holding.Quantity, _ => 1 };
        if (body is HoldingChange hc) ApiFault.Require(hc.RemainingQuantity >= 0 && hc.RemainingQuantity < holding.Quantity, 422, "invalid_quantity", "Only reducing a holding is supported; create or trade to receive items.");
        if (body is not HoldingChange && body is not HoldingDeletion) Quantity(quantity);
        ApiFault.Require(quantity <= holding.Quantity - holding.Reserved, 409, "insufficient_available_quantity", "Not enough unreserved items.");
        string? message = null;
        if (body is ConsumptionRequest use)
        {
            var validChat = use.ChatConsent ? ItemText.Destination(use.Destination) : use.Destination == "";
            ApiFault.Require(CraftingTypes.IsRecipe(holding.TypeId) && !CraftingTypes.IsGear(holding.TypeId) && validChat, 422, "invalid_use",
                "Leave the destination blank or choose a supported destination with chat permission.");
            ApiFault.Require(target == actor, 403, "invalid_use", "You may only use your own items.");
            var revision = await db.DefinitionRevisions.SingleAsync(x => x.CommunityId == communityId && x.DefinitionId == holding.DefinitionId && x.Revision == holding.DefinitionRevision, ct);
            message = revision.UseMessage;
            ApiFault.Require(!use.ChatConsent || ItemText.Chat(message, policy.Value.ChatMessageTextElements), 422, "invalid_message", "Potion message is not safe for posting.");
            if (revision.StatPoints > 0) await combat.GrantPointsAsync(communityId, actor, revision.StatPoints, ct);
            if (StatusEffectService.Decode(revision.StatusEffectJson) is { } effect) await effects.ApplyAsync(communityId, actor, effect, ct);
        }
        holding.Quantity -= quantity; holding.Version++;
        community.InventoryVersion++;
        var op = commands.New(h, scope, body, actor, communityId, body is ConsumptionRequest ? "potion.use" : body is InventoryRemoval || target != actor ? "inventory.remove" : "inventory.discard");
        op.SubjectCharacterId = target; op.Reason = removalReason; op.Summary = $"{op.Kind}: {quantity} × {holding.Name} ({holding.TypeId}).";
        object payload = body is ConsumptionRequest u
            ? new ConsumptionResult(View(holding), message!, u.Destination, u.ChatConsent ? "client_action_required" : "none")
            : View(holding);
        var result = await commands.SaveAsync(h, op, new CommandResult<object>(op.Id, payload), ct); await tx.CommitAsync(ct); return result;
    }
}
