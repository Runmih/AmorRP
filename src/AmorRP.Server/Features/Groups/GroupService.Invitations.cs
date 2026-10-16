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
    public async Task<IResult> InvitationsAsync(HttpContext http, Guid groupId, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); var (group, _) = await access.RequireAsync(groupId, session.CharacterId, ct); GroupAccess.Owner(group, session.CharacterId);
        var scope = $"invitations:{groupId}:{session.CharacterId}"; var (limit, after) = pages.Read(http, scope);
        var query = db.Invitations.Where(x => x.GroupId == groupId);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new InvitationPage(rows.Take(limit).Select(InvitationView).ToArray(), rows.Length > limit ? pages.Write(rows[limit-1].Id, scope) : null, group.Version.ToString(), null));
    }
    private static Invitation InvitationView(InvitationRow x) => new(x.Id, x.GroupId, x.ExpiresAt, x.MaxUses, x.Used, x.Revoked);
    public async Task<IResult> InvitationCommandAsync(HttpContext http, Guid groupId, Guid? invitationId, InvitationCreate? body, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "group:" + groupId], ct);
        var (group, _) = await access.RequireAsync(groupId, session.CharacterId, ct); GroupAccess.Owner(group, session.CharacterId);
        var scope = "character:" + session.CharacterId; var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(http, scope, body, session.CharacterId, groupId, invitationId == null ? "invitation.create" : "invitation.revoke");
        object response;
        if (body != null && invitationId == null)
        {
            ApiFault.Require(body.ExpiresInHours is >= 1 and <= 168 && body.MaxUses is >= 1 and <= 100, 422, "invalid_request", "Invitation bounds are 1–168 hours and 1–100 uses.");
            ApiFault.Require(await db.Invitations.CountAsync(x => x.GroupId == groupId && !x.Revoked && x.ExpiresAt > time.GetUtcNow() && x.Used < x.MaxUses, ct) < policy.Value.MaxActiveInvitationsPerGroup,
                409, "invitation_limit_reached", "Revoke an old invitation before creating more.");
            var code = SecretVault.NewSecret(); var invite = new InvitationRow { GroupId = groupId, CodeHash = SecretVault.Hash(code), MaxUses = body.MaxUses, ExpiresAt = time.GetUtcNow().AddHours(body.ExpiresInHours) };
            db.Invitations.Add(invite); response = new CommandResult<InvitationCreated>(op.Id, new(InvitationView(invite), code));
        }
        else
        {
            var invite = await db.Invitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.GroupId == groupId, ct);
            ApiFault.Require(invite != null, 404, "not_found", "Invitation not found in this group."); invite!.Revoked = true; response = new Receipt(op.Id);
        }
        group.Version++;
        var result = await commands.SaveAsync(http, op, response, ct); await tx.CommitAsync(ct); return result;
    }

}
