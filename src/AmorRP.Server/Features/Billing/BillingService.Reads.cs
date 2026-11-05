using System.Globalization;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Billing;

public sealed partial class BillingService
{
    public async Task<IResult> ListAsync(HttpContext h, Guid communityId, Guid? groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var query = db.Bills.AsNoTracking().Where(x => x.CommunityId == communityId);
        if (groupId.HasValue)
        {
            var group = await GroupAsync(communityId, groupId.Value, ct);
            if (group.Deleted) ApiFault.Require(group.OwnerCharacterId == actor, 403, "owner_required", "Only the former group owner can read these financial records.");
            else
            {
                await communities.RequireAsync(communityId, actor, ct); var member = await GroupAuthorization.MemberAsync(db, group, actor, ct);
                var rights = GroupAuthorization.Effective(group, actor, member);
                ApiFault.Require(rights.Contains(GroupCapabilities.Billing) || rights.Contains(GroupCapabilities.TreasuryRecords), 403, "group_capability_required", "You do not have permission to read this group's bills.");
                if (!rights.Contains(GroupCapabilities.TreasuryRecords)) query = query.Where(x => x.IssuerCharacterId == actor);
            }
            query = query.Where(x => x.GroupId == groupId.Value);
        }
        else
        {
            await communities.TargetAsync(communityId, actor, false, ct);
            query = query.Where(x => x.CharacterId == actor);
        }
        var total = (await query.SumAsync(x => (decimal)(x.Amount - x.Collected), ct)).ToString("0", CultureInfo.InvariantCulture);
        var scope = $"bills:{communityId}:{groupId}:{actor}"; var (limit, after) = pages.Read(h, scope);
        if (after.HasValue)
        {
            var anchor = await query.SingleOrDefaultAsync(x => x.Id == after.Value, ct);
            ApiFault.Require(anchor != null, 422, "invalid_request", "Refresh the bills list before loading this page.");
            query = query.Where(x => x.CreatedAt < anchor!.CreatedAt || x.CreatedAt == anchor.CreatedAt && x.Id.CompareTo(after.Value) < 0);
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new BillPage(rows.Take(limit).Select(View).ToArray(), rows.Length > limit ? pages.Write(rows[limit - 1].Id, scope) : null, total));
    }
    public async Task<IResult> PaymentsAsync(HttpContext h, Guid communityId, Guid billId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var bill = await db.Bills.AsNoTracking().SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == billId, ct);
        ApiFault.Require(bill != null, 404, "not_found", "Bill not found.");
        if (bill!.CharacterId != actor && bill.IssuerCharacterId != actor)
        {
            var group = await GroupAsync(communityId, bill.GroupId, ct);
            if (group.Deleted) ApiFault.Require(group.OwnerCharacterId == actor, 403, "owner_required", "Only the former group owner can read these financial records.");
            else { await communities.RequireAsync(communityId, actor, ct); await GroupAuthorization.ActionAsync(db, group, actor, GroupCapabilities.TreasuryRecords, ct); }
        }
        var scope = $"bill-payments:{communityId}:{billId}:{actor}"; var (limit, after) = pages.Read(h, scope);
        var query = db.BillPayments.AsNoTracking().Where(x => x.CommunityId == communityId && x.BillId == billId);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) < 0);
        var rows = await query.OrderByDescending(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new BillPaymentPage(rows.Take(limit).Select(x => new BillPayment(x.Id, x.BillId, Units(x.Amount), Units(x.CharacterBefore), Units(x.CharacterAfter), Units(x.TreasuryBefore), Units(x.TreasuryAfter), x.Source, x.SourceId, x.OccurredAt)).ToArray(), rows.Length > limit ? pages.Write(rows[limit - 1].Id, scope) : null));
    }
}
