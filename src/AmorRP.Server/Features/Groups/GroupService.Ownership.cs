using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Groups;

public sealed partial class GroupService
{
    public async Task<IResult> OwnershipAsync(HttpContext http, Guid groupId, Guid? transferId, OwnershipProposal? body, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var discovered = transferId.HasValue ? await db.OwnershipTransfers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == transferId && x.GroupId == groupId, ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, new[] { "character:" + session.CharacterId, "character:" + (body?.RecipientCharacterId ?? discovered?.ToCharacterId ?? session.CharacterId),
            "character:" + (discovered?.FromCharacterId ?? session.CharacterId), "group:" + groupId }, ct);
        var (group, _) = await access.RequireAsync(groupId, session.CharacterId, ct);
        OwnershipRow row;
        if (kind == "propose") { GroupAccess.Owner(group, session.CharacterId); row = new OwnershipRow { GroupId = groupId, FromCharacterId = session.CharacterId,
            ToCharacterId = body!.RecipientCharacterId, ExpiresAt = time.GetUtcNow().AddHours(policy.Value.OwnershipProposalHours) }; }
        else
        {
            ApiFault.Require(discovered != null, 404, "not_found", "Ownership proposal not found.");
            row = await db.OwnershipTransfers.SingleAsync(x => x.Id == discovered!.Id, ct);
            ApiFault.Require(group.OwnerCharacterId == session.CharacterId || row.ToCharacterId == session.CharacterId, 404, "not_found", "Ownership proposal not found.");
        }
        if (kind == "read") return Results.Json(OwnershipView(row));
        var scope = "character:" + session.CharacterId; var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(http, scope, body, session.CharacterId, groupId, "ownership." + kind); op.SubjectCharacterId = row.ToCharacterId;
        if (kind == "propose")
        {
            ApiFault.Require(row.ToCharacterId != session.CharacterId, 422, "invalid_request", "Choose another active member.");
            await access.TargetAsync(groupId, row.ToCharacterId, true, ct);
            ApiFault.Require(!await db.OwnershipTransfers.AnyAsync(x => x.GroupId == groupId && x.Status == "proposed" && x.ExpiresAt > time.GetUtcNow(), ct),
                409, "ownership_pending", "Cancel the current ownership proposal first.");
            db.OwnershipTransfers.Add(row);
        }
        else
        {
            ApiFault.Require(row.Status == "proposed" && row.ExpiresAt > time.GetUtcNow() && group.OwnerCharacterId == row.FromCharacterId,
                409, "ownership_unavailable", "Ownership proposal is no longer available.");
            if (kind == "accept")
            {
                ApiFault.Require(row.ToCharacterId == session.CharacterId, 403, "recipient_required", "Only the proposed recipient may accept.");
                var target = await access.TargetAsync(groupId, row.ToCharacterId, true, ct);
                ApiFault.Require(await db.Groups.CountAsync(x => x.OwnerCharacterId == row.ToCharacterId && !x.Deleted, ct) < policy.Value.OwnedGroups,
                    409, "group_limit_reached", "Recipient already owns the maximum number of groups.");
                var former = await access.TargetAsync(groupId, row.FromCharacterId, true, ct); former.Capabilities = []; former.Version++;
                target.Capabilities = []; target.TradingRestricted = false; target.Version++;
                group.OwnerCharacterId = row.ToCharacterId; row.Status = "accepted";
            }
            else row.Status = "cancelled";
        }
        group.Version++;
        var result = await commands.SaveAsync(http, op, new CommandResult<OwnershipTransfer>(op.Id, OwnershipView(row)), ct); await tx.CommitAsync(ct); return result;
    }
    private OwnershipTransfer OwnershipView(OwnershipRow row) => new(row.Id, row.GroupId, row.FromCharacterId, row.ToCharacterId,
        row.Status == "proposed" && row.ExpiresAt <= time.GetUtcNow() ? "expired" : row.Status, row.ExpiresAt);
}
