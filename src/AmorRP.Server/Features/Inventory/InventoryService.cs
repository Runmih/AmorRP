using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Catalog;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Inventory;

public sealed partial class InventoryService(AmorDbContext db, SessionAccess sessions, GroupAccess groups,
    CatalogService catalog, CommandStore commands, PageCursor cursors, TimeProvider time, IOptions<ServicePolicyOptions> policy)
{
    public static Holding View(HoldingRow x) => new(x.Id, x.GroupId, x.CharacterId, x.TypeId, x.Name, x.CategoryId,
        x.Quantity, x.Reserved, x.Quantity - x.Reserved, x.Version, x.DefinitionId, x.DefinitionRevision, x.LetterId);
    private static Quota View(QuotaRow x) => new(x.Kind, x.PeriodStart, x.PeriodStart.AddDays(7), x.Limit, x.Used, x.Limit - x.Used);
    private async Task<QuotaRow> QuotaAsync(GroupRow group, Guid actor, string kind, CancellationToken ct)
    {
        var period = GroupAccess.Period(time.GetUtcNow());
        return await db.Quotas.SingleOrDefaultAsync(x => x.GroupId == group.Id && x.CharacterId == actor && x.Kind == kind && x.PeriodStart == period, ct)
            ?? new QuotaRow { GroupId = group.Id, CharacterId = actor, Kind = kind, PeriodStart = period,
                Limit = kind == "potion_points" ? GroupAccess.Policies(group, time.GetUtcNow()).Current.PotionPoints : GroupAccess.Policies(group, time.GetUtcNow()).Current.Letters };
    }
    private async Task<QuotaRow> SpendAsync(GroupRow group, Guid actor, string kind, int cost, CancellationToken ct)
    {
        var quota = await QuotaAsync(group, actor, kind, ct);
        ApiFault.Require(cost > 0 && cost <= quota.Limit - quota.Used, 409, "quota_exhausted", "Weekly allowance is insufficient. No items were created.");
        if (db.Entry(quota).State == EntityState.Detached) db.Quotas.Add(quota);
        quota.Used += cost; return quota;
    }
    private async Task CapacityAsync(Guid groupId, Guid actor, CancellationToken ct) => ApiFault.Require(
        await db.Holdings.CountAsync(x => x.GroupId == groupId && x.CharacterId == actor && x.Quantity > 0, ct) < policy.Value.MaxHoldingsPerCharacterGroup,
        409, "inventory_full", "Inventory holding limit reached.");
    private async Task<HoldingRow> HoldingAsync(Guid groupId, Guid actor, Guid id, CancellationToken ct)
    {
        var row = await db.Holdings.SingleOrDefaultAsync(x => x.GroupId == groupId && x.CharacterId == actor && x.Id == id && x.Quantity > 0, ct);
        ApiFault.Require(row != null, 404, "not_found", "Holding not found or no longer owned."); return row!;
    }
    private void Quantity(int n, bool creation = false) => ApiFault.Require(n > 0 && n <= (creation ? policy.Value.MaxCreationQuantity : 1000), 422, "invalid_quantity", "Quantity exceeds supported bounds.");
    public async Task<IResult> QuotasAsync(HttpContext h, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; var (group, _) = await groups.RequireAsync(groupId, actor, ct);
        return Results.Json(new Quotas(groupId, actor, [View(await QuotaAsync(group, actor, "potion_points", ct)), View(await QuotaAsync(group, actor, "letters", ct))]));
    }
    public async Task<IResult> ListAsync(HttpContext h, Guid groupId, Guid? target, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; var (group, member) = await groups.RequireAsync(groupId, actor, ct);
        if (target.HasValue && target != actor) { GroupAccess.Action(group, member, "inventory.remove"); await groups.TargetAsync(groupId, target.Value, group.OwnerCharacterId != actor, ct); }
        var owner = target ?? actor;
        var type = h.Request.Query["typeId"].ToString(); var search = h.Request.Query["search"].ToString();
        var typesText = h.Request.Query["typeIds"].ToString();
        var types = typesText.Length == 0 ? Array.Empty<string>() : typesText.Split(',');
        ApiFault.Require(types.Length <= 4 && types.All(x => x is "potion" or "letter" or "book" or "note") && (types.Length == 0 || type.Length == 0), 422, "invalid_request", "Invalid type filters.");
        var categoryText = h.Request.Query["categoryId"].ToString(); var sort = h.Request.Query["sort"].ToString();
        ApiFault.Require(search.Length <= 100 && (type == "" || type is "potion" or "letter" or "book" or "note") && (sort == "" || sort is "name" or "type" or "category"), 422, "invalid_request", "Invalid inventory filters.");
        Guid? category = null;
        if (categoryText.Length > 0) { ApiFault.Require(Guid.TryParse(categoryText, out var parsed), 422, "invalid_request", "Invalid category."); category = parsed; }
        var scope = $"inventory:{groupId}:{actor}:{owner}:{type}:{typesText}:{category}:{search}:{sort}"; var (limit, after) = cursors.Read(h, scope);
        var query = db.Holdings.AsNoTracking().Where(x => x.GroupId == groupId && x.CharacterId == owner && x.Quantity > 0);
        if (type.Length > 0) query = query.Where(x => x.TypeId == type);
        if (types.Length > 0) query = query.Where(x => types.Contains(x.TypeId));
        if (category.HasValue) query = query.Where(x => x.CategoryId == category);
        if (search.Length > 0) query = query.Where(x => x.Name.ToLower().Contains(search.ToLower()));
        var ordered = from holding in query join cat in db.Categories on new { holding.GroupId, Id = holding.CategoryId } equals new { cat.GroupId, cat.Id }
                      select new SortRow { Holding = holding, Term = sort == "type" ? holding.TypeId : sort == "category" ? cat.Name : holding.Name };
        if (after.HasValue)
        {
            // An issued cursor references a retained holding; zero quantities remain for pagination/recovery.
            var anchor = await (from x in db.Holdings where x.GroupId == groupId && x.CharacterId == owner && x.Id == after
                join cat in db.Categories on new { x.GroupId, Id = x.CategoryId } equals new { cat.GroupId, cat.Id }
                select new { Term = sort == "type" ? x.TypeId : sort == "category" ? cat.Name : x.Name, x.Id }).SingleOrDefaultAsync(ct);
            ApiFault.Require(anchor != null, 422, "invalid_request", "Inventory cursor expired. Refresh the list.");
            var term = anchor!.Term; var id = anchor.Id;
            ordered = ordered.Where(x => string.Compare(x.Term, term) > 0 || (x.Term == term && x.Holding.Id.CompareTo(id) > 0));
        }
        var rows = await ordered.OrderBy(x => x.Term).ThenBy(x => x.Holding.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new HoldingPage(rows.Take(limit).Select(x => View(x.Holding)).ToArray(), rows.Length > limit ? cursors.Write(rows[limit - 1].Holding.Id, scope) : null, group.InventoryVersion.ToString()));
    }
    private sealed class SortRow { public HoldingRow Holding { get; set; } = null!; public string Term { get; set; } = ""; }
    public async Task<IResult> DetailsAsync(HttpContext h, Guid groupId, Guid id, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await groups.RequireAsync(groupId, actor, ct); var holding = await HoldingAsync(groupId, actor, id, ct);
        Definition? definition = null; Letter? letter = null;
        if (holding.TypeId == "potion")
        {
            var row = await db.Definitions.SingleAsync(x => x.GroupId == groupId && x.Id == holding.DefinitionId, ct);
            var rev = await db.DefinitionRevisions.SingleAsync(x => x.GroupId == groupId && x.DefinitionId == holding.DefinitionId && x.Revision == holding.DefinitionRevision, ct);
            definition = CatalogService.View(row, rev);
        }
        else letter = await LetterViewAsync(await db.Letters.SingleAsync(x => x.GroupId == groupId && x.Id == holding.LetterId, ct), holding, ct);
        h.Response.Headers.ETag = CommandStore.ETag("holding", holding.Id, holding.Version);
        return Results.Json(new HoldingDetails(View(holding), definition, letter));
    }
    public async Task<IResult> ProduceAsync(HttpContext h, Guid groupId, object body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "group:" + groupId], ct);
        var (group, member) = await groups.RequireAsync(groupId, actor, ct);
        if (body is PotionCreate) GroupAccess.Action(group, member, "items.potion.create");
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        HoldingRow holding; QuotaRow quota;
        if (body is PotionCreate potion)
        {
            Quantity(potion.Quantity, creation: true);
            var row = await db.Definitions.SingleOrDefaultAsync(x => x.GroupId == groupId && x.Id == potion.DefinitionId, ct);
            ApiFault.Require(row != null && !row.Retired, 404, "not_found", "Potion definition unavailable.");
            ApiFault.Require(row!.Revision == potion.ExpectedDefinitionRevision, 409, "definition_changed", "Potion recipe changed. Review the current cost and message.");
            var rev = await db.DefinitionRevisions.SingleAsync(x => x.GroupId == groupId && x.DefinitionId == row.Id && x.Revision == row.Revision, ct);
            ApiFault.Require(rev.Enabled, 409, "definition_unavailable", "Potion production is disabled."); await catalog.RequireCategoryAsync(groupId, rev.CategoryId, "potion", ct);
            var cost = (long)potion.Quantity * rev.CreationCost; ApiFault.Require(cost <= int.MaxValue, 422, "invalid_quantity", "Production cost exceeds supported bounds.");
            quota = await SpendAsync(group, actor, "potion_points", (int)cost, ct);
            holding = await db.Holdings.SingleOrDefaultAsync(x => x.GroupId == groupId && x.CharacterId == actor && x.DefinitionId == row.Id && x.DefinitionRevision == row.Revision, ct)
                ?? new HoldingRow { GroupId = groupId, CharacterId = actor, TypeId = "potion", Name = rev.Name, CategoryId = rev.CategoryId, DefinitionId = row.Id, DefinitionRevision = row.Revision };
            if (holding.Quantity == 0) await CapacityAsync(groupId, actor, ct);
            ApiFault.Require((long)holding.Quantity + potion.Quantity <= int.MaxValue, 409, "quantity_overflow", "Potion stack is full."); holding.Quantity += potion.Quantity;
            if (db.Entry(holding).State == EntityState.Detached) db.Holdings.Add(holding); else holding.Version++;
        }
        else
        {
            var letter = (LetterCreate)body; ApiFault.Require(letter.TypeId is "book" or "letter" or "note", 422, "invalid_type", "Choose book, letter or note.");
            await catalog.RequireCategoryAsync(groupId, letter.CategoryId, letter.TypeId, ct); ValidateLetter(letter.Title, letter.Body); await CapacityAsync(groupId, actor, ct);
            quota = await SpendAsync(group, actor, "letters", 1, ct);
            var row = new LetterRow { GroupId = groupId, AuthorCharacterId = actor, CategoryId = letter.CategoryId, TypeId = letter.TypeId, Title = letter.Title.Trim(), Body = letter.Body }; db.Letters.Add(row);
            holding = new() { GroupId = groupId, CharacterId = actor, TypeId = letter.TypeId, Name = row.Title, CategoryId = row.CategoryId, Quantity = 1, LetterId = row.Id }; db.Holdings.Add(holding);
        }
        group.InventoryVersion++;
        var op = commands.New(h, scope, body, actor, groupId, body is PotionCreate ? "potion.create" : "letter.create"); op.SubjectCharacterId = actor;
        op.Summary = body is PotionCreate pc ? $"Created {pc.Quantity} potion copies: {holding.Name}." : $"Created {holding.TypeId}: {holding.Name}.";
        var result = await commands.SaveAsync(h, op, new CommandResult<ProductionResult>(op.Id, new(View(holding), View(quota))), ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid groupId, Guid? id, object body, CancellationToken ct, Guid? targetCharacterId = null)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "group:" + groupId], ct);
        var (group, member) = await groups.RequireAsync(groupId, actor, ct);
        var target = targetCharacterId ?? (body is InventoryRemoval removal ? removal.TargetCharacterId : actor);
        var removalReason = body switch { HoldingChange x => x.Reason, HoldingDeletion x => x.Reason, InventoryRemoval x => x.Reason, _ => null };
        if (target != actor) { GroupAccess.Action(group, member, "inventory.remove"); ApiFault.Text(removalReason, 500); await groups.TargetAsync(groupId, target, true, ct); }
        if (body is InventoryRemoval rm) { GroupAccess.Action(group, member, "inventory.remove"); ApiFault.Text(rm.Reason, 500); await groups.TargetAsync(groupId, target, true, ct); }
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var holding = await HoldingAsync(groupId, target, id ?? ((InventoryRemoval)body).HoldingId, ct);
        var version = body switch { ConsumptionRequest b => b.ExpectedHoldingVersion, DiscardRequest b => b.ExpectedHoldingVersion, InventoryRemoval b => b.ExpectedHoldingVersion, HoldingChange b => b.ExpectedHoldingVersion, HoldingDeletion b => b.ExpectedHoldingVersion, _ => 0 };
        ApiFault.Require(version == holding.Version, 409, "holding_changed", "Holding changed. Refresh and review its available quantity.");
        var quantity = body switch { DiscardRequest b => b.Quantity, InventoryRemoval b => b.Quantity, HoldingChange b => holding.Quantity - b.RemainingQuantity, HoldingDeletion => holding.Quantity, _ => 1 };
        if (body is HoldingChange hc) ApiFault.Require(hc.RemainingQuantity >= 0 && hc.RemainingQuantity < holding.Quantity, 422, "invalid_quantity", "Only reducing a holding is supported; create or trade to receive items.");
        if (body is not HoldingChange && body is not HoldingDeletion) Quantity(quantity);
        ApiFault.Require(quantity <= holding.Quantity - holding.Reserved, 409, "insufficient_available_quantity", "Not enough unreserved items.");
        string? message = null;
        if (body is ConsumptionRequest use)
        {
            ApiFault.Require(holding.TypeId == "potion" && use.ChatConsent && ItemText.Destination(use.Destination), 422, "invalid_use", "Potion use requires startup chat permission and an explicit supported destination.");
            message = (await db.DefinitionRevisions.SingleAsync(x => x.GroupId == groupId && x.DefinitionId == holding.DefinitionId && x.Revision == holding.DefinitionRevision, ct)).UseMessage;
            ApiFault.Require(ItemText.Chat(message, policy.Value.ChatMessageTextElements), 422, "invalid_message", "Potion message is not safe for posting.");
        }
        holding.Quantity -= quantity; holding.Version++;
        group.InventoryVersion++;
        var op = commands.New(h, scope, body, actor, groupId, body is ConsumptionRequest ? "potion.use" : body is InventoryRemoval || target != actor ? "inventory.remove" : "inventory.discard");
        op.SubjectCharacterId = target; op.Reason = removalReason; op.Summary = $"{op.Kind}: {quantity} × {holding.Name} ({holding.TypeId}).";
        object payload = body is ConsumptionRequest u ? new ConsumptionResult(View(holding), message!, u.Destination) : View(holding);
        var result = await commands.SaveAsync(h, op, new CommandResult<object>(op.Id, payload), ct); await tx.CommitAsync(ct); return result;
    }
}
