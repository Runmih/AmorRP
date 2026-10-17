using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Communities;

public sealed partial class CommunityService
{
    public async Task<IResult> OwnershipAsync(HttpContext http, Guid communityId, Guid? transferId, OwnershipProposal? body, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var discovered = transferId.HasValue ? await db.OwnershipTransfers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == transferId && x.CommunityId == communityId, ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, new[] { "character:" + session.CharacterId, "character:" + (body?.RecipientCharacterId ?? discovered?.ToCharacterId ?? session.CharacterId),
            "character:" + (discovered?.FromCharacterId ?? session.CharacterId), "community:" + communityId }, ct);
        var (community, _) = await access.RequireAsync(communityId, session.CharacterId, ct);
        OwnershipRow row;
        if (kind == "propose") { CommunityAccess.Owner(community, session.CharacterId); row = new OwnershipRow { CommunityId = communityId, FromCharacterId = session.CharacterId,
            ToCharacterId = body!.RecipientCharacterId, ExpiresAt = time.GetUtcNow().AddHours(policy.Value.OwnershipProposalHours) }; }
        else
        {
            ApiFault.Require(discovered != null, 404, "not_found", "Ownership proposal not found.");
            row = await db.OwnershipTransfers.SingleAsync(x => x.Id == discovered!.Id, ct);
            ApiFault.Require(community.OwnerCharacterId == session.CharacterId || row.ToCharacterId == session.CharacterId, 404, "not_found", "Ownership proposal not found.");
        }
        if (kind == "read") return Results.Json(OwnershipView(row));
        var scope = "character:" + session.CharacterId; var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(http, scope, body, session.CharacterId, communityId, "ownership." + kind); op.SubjectCharacterId = row.ToCharacterId;
        if (kind == "propose")
        {
            ApiFault.Require(row.ToCharacterId != session.CharacterId, 422, "invalid_request", "Choose another active member.");
            await access.TargetAsync(communityId, row.ToCharacterId, true, ct);
            ApiFault.Require(!await db.OwnershipTransfers.AnyAsync(x => x.CommunityId == communityId && x.Status == "proposed" && x.ExpiresAt > time.GetUtcNow(), ct),
                409, "ownership_pending", "Cancel the current ownership proposal first.");
            db.OwnershipTransfers.Add(row);
        }
        else
        {
            ApiFault.Require(row.Status == "proposed" && row.ExpiresAt > time.GetUtcNow() && community.OwnerCharacterId == row.FromCharacterId,
                409, "ownership_unavailable", "Ownership proposal is no longer available.");
            if (kind == "accept")
            {
                ApiFault.Require(row.ToCharacterId == session.CharacterId, 403, "recipient_required", "Only the proposed recipient may accept.");
                var target = await access.TargetAsync(communityId, row.ToCharacterId, true, ct);
                ApiFault.Require(await db.Communities.CountAsync(x => x.OwnerCharacterId == row.ToCharacterId && !x.Deleted, ct) < policy.Value.OwnedCommunities,
                    409, "community_limit_reached", "Recipient already owns the maximum number of communities.");
                var former = await access.TargetAsync(communityId, row.FromCharacterId, true, ct); former.Capabilities = []; former.Version++;
                target.Capabilities = []; target.TradingRestricted = false; target.Version++;
                community.OwnerCharacterId = row.ToCharacterId; row.Status = "accepted";
            }
            else row.Status = "cancelled";
        }
        community.Version++;
        var result = await commands.SaveAsync(http, op, new CommandResult<OwnershipTransfer>(op.Id, OwnershipView(row)), ct); await tx.CommitAsync(ct); return result;
    }
    private OwnershipTransfer OwnershipView(OwnershipRow row) => new(row.Id, row.CommunityId, row.FromCharacterId, row.ToCharacterId,
        row.Status == "proposed" && row.ExpiresAt <= time.GetUtcNow() ? "expired" : row.Status, row.ExpiresAt);
}
