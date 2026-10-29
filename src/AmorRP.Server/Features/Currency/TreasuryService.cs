using System.Globalization;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Currency;

public sealed class TreasuryService(AmorDbContext db, SessionAccess sessions, CommunityAccess communities, CommandStore commands, TreasuryLedger ledger, PageCursor pages)
{
    private async Task<GroupRow> GroupAsync(Guid communityId, Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == groupId, ct);
        ApiFault.Require(group != null, 404, "not_found", "Group not found in this community."); return group!;
    }
    private async Task AuthorizeAsync(GroupRow group, Guid actor, string? capability, CancellationToken ct)
    {
        await communities.RequireAsync(group.CommunityId, actor, ct);
        ApiFault.Require(!group.Deleted, 404, "not_found", "This group has disbanded.");
        await GroupAuthorization.MemberAsync(db, group, actor, ct);
        ApiFault.Require(group.EnabledFeatures.Contains(GroupCapabilities.Treasury), 403, "group_feature_disabled", "The community owner has not enabled this group's treasury.");
        if (capability != null) await GroupAuthorization.ActionAsync(db, group, actor, capability, ct);
    }
    public async Task<IResult> BalanceAsync(HttpContext h, Guid communityId, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var group = await GroupAsync(communityId, groupId, ct); await AuthorizeAsync(group, actor, null, ct);
        var community = await db.Communities.SingleAsync(x => x.Id == communityId, ct);
        return Results.Json(TreasuryLedger.Balance(group, community));
    }
    public async Task<IResult> RecordsAsync(HttpContext h, Guid communityId, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var group = await GroupAsync(communityId, groupId, ct);
        if (group.Deleted) ApiFault.Require(group.OwnerCharacterId == actor, 403, "owner_required", "Only the former group owner can view its retained treasury records.");
        else await AuthorizeAsync(group, actor, GroupCapabilities.TreasuryRecords, ct);
        var scope = $"treasury-records:{communityId}:{groupId}:{actor}"; var (limit, after) = pages.Read(h, scope);
        var query = db.TreasuryEntries.AsNoTracking().Where(x => x.CommunityId == communityId && x.GroupId == groupId);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) < 0);
        var rows = await query.OrderByDescending(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new TreasuryReceiptPage(rows.Take(limit).Select(TreasuryLedger.View).ToArray(), rows.Length > limit ? pages.Write(rows[limit - 1].Id, scope) : null));
    }
    public async Task<IResult> TransferAsync(HttpContext h, Guid communityId, Guid groupId, TreasuryTransfer body, bool deposit, CancellationToken ct)
    {
        ApiFault.Require(WholeUnits.TryNormalize(body.Amount, out _, out var amount) && amount > 0, 422, "invalid_quantity", "Enter a positive whole number of coins.");
        ApiFault.Require(PlainText.Error(body.Note, 300, required: false) == null, 422, "invalid_request", "The note must contain at most 300 plain-text characters.");
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var group = await GroupAsync(communityId, groupId, ct);
        await AuthorizeAsync(group, actor, deposit ? null : GroupCapabilities.TreasuryWithdraw, ct);
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        var kind = deposit ? "deposit" : "withdrawal";
        var op = commands.New(h, scope, body, actor, communityId, "treasury." + kind);
        var entry = await ledger.MoveAsync(group, member, amount, kind, op, ct, body.Note);
        op.SubjectCharacterId = actor; op.Delta = deposit ? -amount : amount; op.BeforeBalance = entry.CharacterBefore; op.AfterBalance = entry.CharacterAfter;
        op.Summary = deposit ? $"Deposited {body.Amount} coins into {group.Name}." : $"Withdrew {body.Amount} coins from {group.Name}.";
        var result = await commands.SaveAsync(h, op, new CommandResult<TreasuryTransferResult>(op.Id, new(TreasuryLedger.Balance(group, community), CommunityAccess.Balance(community, member), TreasuryLedger.View(entry))), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> ReturnsAsync(HttpContext h, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var scope = "treasury-returns:" + actor; var (limit, after) = pages.Read(h, scope);
        var query = from g in db.Groups.AsNoTracking() join community in db.Communities on g.CommunityId equals community.Id
            where g.Deleted && g.OwnerCharacterId == actor && g.TreasuryOwned > 0
            select new { Group = g, Community = community };
        if (after.HasValue) query = query.Where(x => x.Group.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Group.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new TreasuryReturnPage(rows.Take(limit).Select(x => new TreasuryReturn(x.Group.CommunityId, x.Group.Id, x.Community.Name, x.Group.Name, x.Community.CurrencyName, x.Group.TreasuryOwned.ToString(CultureInfo.InvariantCulture))).ToArray(), rows.Length > limit ? pages.Write(rows[limit - 1].Group.Id, scope) : null));
    }
    public async Task<IResult> CollectAsync(HttpContext h, Guid communityId, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var group = await GroupAsync(communityId, groupId, ct);
        ApiFault.Require(group.Deleted && group.OwnerCharacterId == actor, 403, "owner_required", "Only the former group owner can collect this return.");
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, null, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(h, scope, null, actor, communityId, "treasury.return");
        var entry = await ledger.ReturnToOwnerAsync(group, op, ct);
        ApiFault.Require(entry != null, 409, "treasury_return_unavailable", "No coins can be returned now. Your balance may be full; spend some coins and try again.");
        var community = await db.Communities.SingleAsync(x => x.Id == communityId, ct);
        var member = await db.Memberships.SingleAsync(x => x.CommunityId == communityId && x.CharacterId == actor, ct);
        op.SubjectCharacterId = actor; op.Summary = "Collected a disbanded group's treasury return.";
        var result = await commands.SaveAsync(h, op, new CommandResult<TreasuryTransferResult>(op.Id, new(TreasuryLedger.Balance(group, community), CommunityAccess.Balance(community, member), TreasuryLedger.View(entry!))), ct);
        await tx.CommitAsync(ct); return result;
    }
}
