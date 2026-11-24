using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Inventory;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Groups;

public sealed class GroupResources(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons,
    InventoryDelivery delivery, CommandStore commands, TimeProvider time)
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/storage", (HttpContext h, Guid seasonId, Guid groupId, GroupResources s, CancellationToken ct) => s.StorageAsync(h, seasonId, groupId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/storage", (HttpContext h, Guid seasonId, Guid groupId, GroupStockMove body, GroupResources s, CancellationToken ct) => s.MoveAsync(h, seasonId, groupId, body, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/shifts", (HttpContext h, Guid seasonId, Guid groupId, GroupResources s, CancellationToken ct) => s.ShiftsAsync(h, seasonId, groupId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/shifts", (HttpContext h, Guid seasonId, Guid groupId, GroupShiftCreate body, GroupResources s, CancellationToken ct) => s.CreateShiftAsync(h, seasonId, groupId, body, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}/shifts/{shiftId}", (HttpContext h, Guid seasonId, Guid groupId, Guid shiftId, GroupResources s, CancellationToken ct) => s.CancelShiftAsync(h, seasonId, groupId, shiftId, ct));
    }

    private async Task<GroupRow> GroupAsync(Guid season, Guid group, Guid actor, CancellationToken ct)
    {
        await seasons.RequireAsync(season, actor, ct);
        var row = await db.Groups.SingleOrDefaultAsync(g => g.CommunityId == season && g.Id == group && !g.Deleted, ct);
        ApiFault.Require(row != null, 404, "not_found", "Group unavailable.");
        return row!;
    }

    private async Task<IResult> StorageAsync(HttpContext h, Guid season, Guid group, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var row = await GroupAsync(season, group, actor, ct);
        var member = await GroupAuthorization.MemberAsync(db, row, actor, ct);
        ApiFault.Require(GroupAuthorization.Effective(row, actor, member).Any(x => x is GroupCapabilities.Storage or GroupCapabilities.StorageWithdraw),
            403, "group_capability_required", "You do not have access to group storage.");
        return Results.Json(new GroupStorage(await (from stock in db.GroupStock.AsNoTracking()
            join revision in db.DefinitionRevisions on new { stock.CommunityId, stock.DefinitionId, stock.Revision }
                equals new { revision.CommunityId, revision.DefinitionId, revision.Revision }
            join definition in db.Definitions on new { stock.CommunityId, Id = stock.DefinitionId }
                equals new { definition.CommunityId, definition.Id }
            where stock.CommunityId == season && stock.GroupId == row.Id
            orderby revision.Name, stock.Id
            select new GroupStock(stock.Id, revision.Name, definition.TypeId, stock.Quantity, stock.Version)).ToArrayAsync(ct)));
    }

    private Task<IResult> MoveAsync(HttpContext h, Guid season, Guid group, GroupStockMove body, CancellationToken ct) =>
        ChangeAsync(h, season, group, body, body.Withdraw ? "group.storage.withdraw" : "group.storage.deposit",
            body.Withdraw ? GroupCapabilities.StorageWithdraw : GroupCapabilities.Storage, async (row, actor, operation) =>
        {
            ApiFault.Require(body.Quantity > 0, 422, "invalid_quantity", "Choose a positive quantity.");
            if (body.Withdraw)
            {
                var stock = await db.GroupStock.SingleOrDefaultAsync(x => x.CommunityId == season && x.GroupId == group && x.Id == body.ItemId, ct);
                ApiFault.Require(stock != null && stock.Version == body.Version && stock.Quantity >= body.Quantity,
                    409, "stock_changed", "Storage changed. Refresh and review the amount.");
                await DeliverAsync(stock!, actor, body.Quantity, ct);
                stock!.Quantity -= body.Quantity;
                if (stock.Quantity == 0) db.GroupStock.Remove(stock);
                else stock.Version++;
            }
            else
            {
                var holding = await db.Holdings.SingleOrDefaultAsync(x => x.CommunityId == season && x.CharacterId == actor
                    && x.Id == body.ItemId, ct);
                ApiFault.Require(holding?.DefinitionId != null && holding.DefinitionRevision != null
                    && holding.Version == body.Version && holding.Quantity - holding.Reserved >= body.Quantity,
                    409, "holding_changed", "Select available, unreserved copies of a stackable item.");
                var definition = holding!.DefinitionId!.Value;
                var revision = holding.DefinitionRevision!.Value;
                var stock = await db.GroupStock.SingleOrDefaultAsync(x => x.CommunityId == season && x.GroupId == group
                    && x.DefinitionId == definition && x.Revision == revision, ct);
                if (stock == null)
                {
                    ApiFault.Require(await db.GroupStock.CountAsync(x => x.CommunityId == season && x.GroupId == group, ct) < 200,
                        409, "storage_full", "Group storage can hold up to 200 stacks.");
                    stock = new GroupStockRow { CommunityId = season, GroupId = group, DefinitionId = definition, Revision = revision };
                    db.GroupStock.Add(stock);
                }
                ApiFault.Require((long)stock.Quantity + body.Quantity <= int.MaxValue, 409, "quantity_overflow", "The storage stack is full.");
                holding.Quantity -= body.Quantity;
                holding.Version++;
                stock.Quantity += body.Quantity;
                stock.Version++;
            }
            var community = await db.Seasons.SingleAsync(s => s.Id == season, ct);
            community.InventoryVersion++;
            operation.Summary = $"{(body.Withdraw ? "Withdrew" : "Deposited")} {body.Quantity} item(s) in {row.Name}.";
            return body.ItemId;
        }, ct);

    public async Task ReturnStockAsync(GroupRow group, CancellationToken ct)
    {
        var stock = await db.GroupStock.Where(x => x.CommunityId == group.CommunityId && x.GroupId == group.Id).ToArrayAsync(ct);
        foreach (var item in stock)
        {
            await DeliverAsync(item, group.OwnerCharacterId, item.Quantity, ct);
            db.GroupStock.Remove(item);
            // Keep inventory capacity checks accurate for the next stack within the same transaction.
            await db.SaveChangesAsync(ct);
        }
        if (stock.Length > 0) (await db.Seasons.SingleAsync(s => s.Id == group.CommunityId, ct)).InventoryVersion++;
    }

    private async Task DeliverAsync(GroupStockRow stock, Guid actor, int quantity, CancellationToken ct)
    {
        var definition = await db.Definitions.SingleAsync(d => d.CommunityId == stock.CommunityId && d.Id == stock.DefinitionId, ct);
        var revision = await db.DefinitionRevisions.SingleAsync(r => r.CommunityId == stock.CommunityId
            && r.DefinitionId == stock.DefinitionId && r.Revision == stock.Revision, ct);
        await delivery.AddAsync(stock.CommunityId, actor, definition, revision, quantity, ct);
    }

    private async Task<IResult> ShiftsAsync(HttpContext h, Guid season, Guid group, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var row = await GroupAsync(season, group, actor, ct);
        await GroupAuthorization.ActionAsync(db, row, actor, GroupCapabilities.Billing, ct);
        var now = time.GetUtcNow();
        return Results.Json(new GroupShifts(await (from shift in db.GroupShifts.AsNoTracking()
            join character in db.Characters on shift.CharacterId equals character.Id
            where shift.CommunityId == season && shift.GroupId == row.Id && shift.End > now
                && (actor == row.OwnerCharacterId || shift.CharacterId == actor)
            orderby shift.Start, shift.Id
            select new GroupShift(shift.Id, shift.CharacterId, character.DisplayName, shift.Start, shift.End)).ToArrayAsync(ct)));
    }

    private Task<IResult> CreateShiftAsync(HttpContext h, Guid season, Guid group, GroupShiftCreate body, CancellationToken ct) =>
        ChangeAsync(h, season, group, body, "group.shift.create", GroupCapabilities.Billing, async (row, actor, operation) =>
        {
            GroupActivityService.Owner(row, actor);
            await GroupAuthorization.ActionAsync(db, row, body.CharacterId, GroupCapabilities.Billing, ct);
            var now = time.GetUtcNow();
            var start = body.Start.ToUniversalTime();
            var end = body.End.ToUniversalTime();
            ApiFault.Require(start >= now && start <= now.AddDays(180) && end > start && end - start <= TimeSpan.FromHours(24),
                422, "invalid_shift", "Choose a future shift up to 24 hours long within the next 180 days.");
            var shifts = db.GroupShifts.Where(x => x.CommunityId == season && x.GroupId == group && x.End > now);
            ApiFault.Require(await shifts.CountAsync(ct) < 200
                && !await shifts.AnyAsync(x => x.CharacterId == body.CharacterId && x.Start < end && x.End > start, ct),
                409, "shift_conflict", "This member already has an overlapping shift, or the schedule is full.");
            var shift = new GroupShiftRow { CommunityId = season, GroupId = group, CharacterId = body.CharacterId, Start = start, End = end };
            db.GroupShifts.Add(shift);
            operation.SubjectCharacterId = body.CharacterId;
            operation.Summary = "Scheduled a staff shift.";
            return shift.Id;
        }, ct);

    private Task<IResult> CancelShiftAsync(HttpContext h, Guid season, Guid group, Guid shiftId, CancellationToken ct) =>
        ChangeAsync(h, season, group, null, "group.shift.cancel", GroupCapabilities.Billing, async (row, actor, operation) =>
        {
            GroupActivityService.Owner(row, actor);
            var shift = await db.GroupShifts.SingleOrDefaultAsync(x => x.CommunityId == season && x.GroupId == group && x.Id == shiftId, ct);
            ApiFault.Require(shift != null, 404, "not_found", "Shift unavailable.");
            db.GroupShifts.Remove(shift!);
            operation.SubjectCharacterId = shift!.CharacterId;
            operation.Summary = "Cancelled a staff shift.";
            return shiftId;
        }, ct);

    private async Task<IResult> ChangeAsync(HttpContext h, Guid season, Guid group, object? body, string kind, string permission,
        Func<GroupRow, Guid, OperationRow, Task<Guid>> change, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var scope = "character:" + actor;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [scope, "season:" + season], ct);
        var row = await GroupAsync(season, group, actor, ct);
        await GroupAuthorization.ActionAsync(db, row, actor, permission, ct);
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var operation = commands.New(h, scope, body, actor, season, kind);
        var id = await change(row, actor, operation);
        var result = await commands.SaveAsync(h, operation, new CommandResult<Guid>(operation.Id, id), ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
