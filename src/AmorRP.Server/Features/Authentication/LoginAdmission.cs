using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Authentication;

public sealed class LoginAdmission(AmorDbContext db, SecretVault vault, IOptions<SessionPolicyOptions> policy, TimeProvider time)
{
    // Called after idempotent replay, inside the caller's transaction and existing
    // database-wide auth lock. Count and insertion are serialized across replicas.
    public async Task<string> CheckAsync(HttpContext http, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var hashes = vault.LoginOriginHashes(ClientNetwork.Key(http));
        var active = db.Logins.Where(x => x.SessionId == null && x.ExpiresAt > now && (x.Status == "pending" || x.Status == "verified"));
        ApiFault.Require(await active.CountAsync(x => hashes.Contains(x.OriginHash!), ct) < policy.Value.MaxPendingLoginsPerNetwork,
            429, "rate_limited", "Too many unfinished logins from this network. Finish or cancel a login and try again.");
        var since = now.AddMinutes(-1);
        ApiFault.Require(await db.Logins.CountAsync(x => hashes.Contains(x.OriginHash!) && x.StartedAt > since, ct) < policy.Value.LoginStartsPerMinutePerNetwork,
            429, "rate_limited", "Too many new logins from this network. Try again shortly.");
        ApiFault.Require(await active.CountAsync(ct) < policy.Value.MaxPendingLogins,
            429, "rate_limited", "Too many pending logins. Try again shortly.");
        return hashes[0];
    }

    public Task<int> ClearExpiredOriginsAsync(CancellationToken ct)
        => db.Logins.Where(x => x.OriginHash != null && x.ExpiresAt <= time.GetUtcNow())
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.OriginHash, (string?)null), ct);
}
