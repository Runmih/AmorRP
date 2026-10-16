using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Authentication;

public sealed class SessionAccess(AmorDbContext db, TimeProvider time)
{
    public async Task<SessionRow> GetAsync(HttpContext context, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        var header = context.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : "";
        ApiFault.Require(SecretVault.IsCredential(token), 401, "authentication_required", "Sign into your character.");
        var hash = SecretVault.Hash(token);
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.AccessHash == hash, ct);
        ApiFault.Require(session != null && !session.Revoked && session.AccessExpiresAt > time.GetUtcNow(),
            401, "authentication_required", "Session expired or revoked. Renew your login.");
        return session!;
    }
    public static Character View(CharacterRow row) => new(row.Id, row.DisplayName, row.HomeWorldId,
        row.HomeWorldName, row.VerifiedAt, "active");
}
