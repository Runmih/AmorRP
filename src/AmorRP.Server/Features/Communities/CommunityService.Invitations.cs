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
    public async Task<IResult> InvitationsAsync(HttpContext http, Guid communityId, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); var (community, _) = await access.RequireAsync(communityId, session.CharacterId, ct); CommunityAccess.Owner(community, session.CharacterId);
        var scope = $"invitations:{communityId}:{session.CharacterId}"; var (limit, after) = pages.Read(http, scope);
        var query = db.Invitations.Where(x => x.CommunityId == communityId);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new InvitationPage(rows.Take(limit).Select(InvitationView).ToArray(), rows.Length > limit ? pages.Write(rows[limit-1].Id, scope) : null, community.Version.ToString(), null));
    }
    private static Invitation InvitationView(InvitationRow x) => new(x.Id, x.CommunityId, x.ExpiresAt, x.MaxUses, x.Used, x.Revoked);
    public async Task<IResult> InvitationCommandAsync(HttpContext http, Guid communityId, Guid? invitationId, InvitationCreate? body, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "community:" + communityId], ct);
        var (community, _) = await access.RequireAsync(communityId, session.CharacterId, ct); CommunityAccess.Owner(community, session.CharacterId);
        var scope = "character:" + session.CharacterId; var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(http, scope, body, session.CharacterId, communityId, invitationId == null ? "invitation.create" : "invitation.revoke");
        object response;
        if (body != null && invitationId == null)
        {
            ApiFault.Require(await db.Invitations.CountAsync(x => x.CommunityId == communityId && !x.Revoked && x.ExpiresAt > time.GetUtcNow() && x.Used < x.MaxUses, ct) < policy.Value.MaxActiveInvitationsPerCommunity,
                409, "invitation_limit_reached", "Delete an old invitation before creating more.");
            string code;
            do { code = InvitationCode.New(); } while (await db.Invitations.AnyAsync(x => x.CodeHash == SecretVault.Hash(code), ct));
            var invite = new InvitationRow { CommunityId = communityId, CodeHash = SecretVault.Hash(code), MaxUses = int.MaxValue, ExpiresAt = InvitationCode.PermanentUntil };
            db.Invitations.Add(invite); response = new CommandResult<InvitationCreated>(op.Id, new(InvitationView(invite), code));
        }
        else
        {
            var invite = await db.Invitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.CommunityId == communityId, ct);
            ApiFault.Require(invite != null, 404, "not_found", "Invitation not found in this community."); invite!.Revoked = true; response = new Receipt(op.Id);
        }
        community.Version++;
        var result = await commands.SaveAsync(http, op, response, ct); await tx.CommitAsync(ct); return result;
    }

}
