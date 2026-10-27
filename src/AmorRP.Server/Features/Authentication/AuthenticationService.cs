using System.Security.Cryptography;
using AmorRP.Server.Options;
using System.Text;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Feasibility;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Authentication;

public sealed class AuthenticationService(AmorDbContext db, SecretVault vault, CommandStore commands,
    SessionAccess access, IDurableIdentityProvider provider, IOptions<XivAuthOptions> options, IOptions<SessionPolicyOptions> sessionPolicy, TimeProvider time, LoginAdmission admission)
{
    private void Enabled() => ApiFault.Require(options.Value.DurableEnabled, 503, "authentication_unavailable", "Durable XIVAuth login is not configured.");
    public async Task<IResult> StartAsync(HttpContext http, LoginStart body, CancellationToken ct)
    {
        Enabled(); ApiFault.Text(body.DisplayName, 80); ApiFault.Text(body.HomeWorldName, 80); ApiFault.Text(body.HomeWorldId, 10);
        ApiFault.Require(uint.TryParse(body.HomeWorldId, out var world) && world > 0, 422, "invalid_request", "Invalid home world.");
        var credential = http.Request.Headers["X-Login-Request-Credential"].ToString();
        ApiFault.Require(SecretVault.IsCredential(credential), 400, "invalid_request", "Login request credential is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var scope = "login:" + SecretVault.Hash(credential);
        await CommandStore.LockAsync(db, ["auth", scope], ct);
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null)
        {
            var attempt = await db.Logins.SingleOrDefaultAsync(x => x.Id == replay.SubjectCharacterId, ct);
            ApiFault.Require(attempt != null && attempt.Status == "pending" && attempt.ExpiresAt > time.GetUtcNow(), 409, "login_expired", "Start another login.");
            return commands.Replay(replay);
        }
        var originHash = await admission.CheckAsync(http, ct);
        CharacterRow? existing = null;
        if (body.KnownCharacterId.HasValue)
            existing = await db.Characters.SingleOrDefaultAsync(x => x.Id == body.KnownCharacterId, ct);
        ApiFault.Require(!body.KnownCharacterId.HasValue || existing != null, 404, "not_found", "Saved character was not found.");
        var state = SecretVault.NewSecret(); var verifier = SecretVault.NewSecret(); var secret = SecretVault.NewSecret();
        var row = new LoginRow { CredentialHash = SecretVault.Hash(secret), StateHash = SecretVault.Hash(state),
            ProtectedVerifier = vault.Protect(verifier, "pkce"), DisplayName = body.DisplayName, HomeWorldId = body.HomeWorldId,
            HomeWorldName = body.HomeWorldName, ExistingCharacterId = existing?.Id, ExpiresAt = time.GetUtcNow().AddMinutes(sessionPolicy.Value.LoginMinutes),
            StartedAt = time.GetUtcNow(), OriginHash = originHash };
        db.Logins.Add(row);
        var op = commands.New(http, scope, body, null, null, "login.start"); op.SubjectCharacterId = row.Id;
        var url = provider.AuthorizationUrl(state, WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        var result = await commands.SaveAsync(http, op, new CommandResult<LoginAttemptCreated>(op.Id, new(row.Id, url, secret, row.ExpiresAt, 3, PairingRequired: true)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> CallbackAsync(HttpContext http, CancellationToken ct)
    {
        Enabled(); http.Response.Headers.CacheControl = "no-store"; http.Response.Headers["Referrer-Policy"] = "no-referrer";
        http.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        var state = http.Request.Query["state"].ToString();
        ApiFault.Require(SecretVault.IsCredential(state), 400, "invalid_request", "Invalid login callback.");
        // Consume state in its own transaction before any provider call, including a timeout.
        Guid attemptId;
        await using (var claim = await db.Database.BeginTransactionAsync(ct))
        {
            await CommandStore.LockAsync(db, ["auth"], ct);
            var attempt = await db.Logins.SingleOrDefaultAsync(x => x.StateHash == SecretVault.Hash(state), ct);
            ApiFault.Require(attempt != null && attempt.Status == "pending" && !attempt.StateUsed && attempt.ExpiresAt > time.GetUtcNow(),
                400, "login_expired", "Login callback is expired or already used.");
            attempt!.StateUsed = true; attemptId = attempt.Id;
            await db.SaveChangesAsync(ct); await claim.CommitAsync(ct);
        }
        var row = await db.Logins.SingleAsync(x => x.Id == attemptId, ct);
        string? pairingCode = null;
        ProviderGrant? grant = null;
        var failure = "identity_not_verified";
        try
        {
            var existing = row.ExistingCharacterId.HasValue ? await db.Characters.SingleAsync(x => x.Id == row.ExistingCharacterId, ct) : null;
            var code = http.Request.Query["code"].ToString();
            if (code.Length is > 0 and <= 4096 && !http.Request.Query.ContainsKey("error"))
                grant = await provider.ExchangeAsync(code, vault.Unprotect(row.ProtectedVerifier, "pkce"),
                    new(row.DisplayName, row.HomeWorldId, row.HomeWorldName, row.ExistingCharacterId), existing?.LodestoneId, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        { failure = "identity_provider_unavailable"; }
        await using var tx = await db.Database.BeginTransactionAsync(CancellationToken.None);
        await CommandStore.LockAsync(db, ["auth"], CancellationToken.None);
        await db.Entry(row).ReloadAsync(CancellationToken.None);
        if (row.Status == "pending" && row.ExpiresAt > time.GetUtcNow() && grant != null)
        {
            var character = await db.Characters.SingleOrDefaultAsync(x => x.LodestoneId == grant.Identity.LodestoneId, CancellationToken.None);
            var hash = SecretVault.Hash(grant.Identity.OwnershipKey);
            if (character != null && character.OwnershipKeyHash != hash)
                failure = "identity_binding_changed";
            else if (row.ExistingCharacterId.HasValue && character?.Id != row.ExistingCharacterId)
                failure = "identity_binding_changed";
            else
            {
                character ??= new CharacterRow { LodestoneId = grant.Identity.LodestoneId, OwnershipKeyHash = hash };
                if (db.Entry(character).State == EntityState.Detached) db.Characters.Add(character);
                character.DisplayName = grant.Identity.DisplayName; character.HomeWorldName = grant.Identity.HomeWorldName;
                character.HomeWorldId = row.HomeWorldId; character.VerifiedAt = time.GetUtcNow();
                row.CharacterId = character.Id; row.ProtectedProviderRefresh = vault.Protect(grant.RefreshToken, "provider"); row.Status = "verified";
                pairingCode = LoginPairingCode.Create(); row.PairingCodeHash = SecretVault.Hash(pairingCode); row.PairingFailures = 0;
            }
        }
        if (row.Status == "pending") { row.Status = "failed"; row.FailureCode = failure; }
        row.ProtectedVerifier = "";
        await db.SaveChangesAsync(CancellationToken.None); await tx.CommitAsync(CancellationToken.None);
        return Browser.LoginResultPage.Render(http, row.Status == "verified", pairingCode: pairingCode);
    }
    private async Task<LoginRow> AttemptAsync(HttpContext http, Guid id, CancellationToken ct)
    {
        var secret = http.Request.Headers["X-Login-Attempt-Credential"].ToString();
        ApiFault.Require(SecretVault.IsCredential(secret), 401, "authentication_required", "Login attempt credential is required.");
        var row = await db.Logins.SingleOrDefaultAsync(x => x.Id == id && x.CredentialHash == SecretVault.Hash(secret), ct);
        ApiFault.Require(row != null, 404, "not_found", "Login attempt not found.");
        ApiFault.Require(row!.ExpiresAt > time.GetUtcNow(), 409, "login_expired", "Login attempt expired. Start again.");
        http.Response.Headers.CacheControl = "no-store"; return row;
    }
    public async Task<IResult> StatusAsync(HttpContext http, Guid id, CancellationToken ct)
    {
        var row = await AttemptAsync(http, id, ct);
        return Results.Json(new LoginAttemptStatus(row.Id, row.Status, row.ExpiresAt, row.FailureCode));
    }
    public async Task<IResult> ExchangeAsync(HttpContext http, Guid id, bool cancel, CancellationToken ct, LoginExchangeRequest? body = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CommandStore.LockAsync(db, ["auth"], ct);
        var row = await AttemptAsync(http, id, ct);
        var scope = "attempt:" + row.Id;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null)
        {
            if (!cancel) ApiFault.Require(row.SessionId.HasValue && await db.Sessions.AnyAsync(x => x.Id == row.SessionId && !x.Revoked, ct),
                401, "authentication_required", "Session was revoked.");
            return commands.Replay(replay);
        }
        var op = commands.New(http, scope, body, row.CharacterId, null, cancel ? "login.cancel" : "login.exchange");
        IResult result;
        if (cancel)
        {
            ApiFault.Require(row.SessionId == null, 409, "login_redeemed", "Login was already redeemed.");
            row.Status = "cancelled"; row.ProtectedVerifier = ""; row.ProtectedProviderRefresh = null; row.PairingCodeHash = null;
            result = await commands.SaveAsync(http, op, new Receipt(op.Id), ct);
        }
        else
        {
            ApiFault.Require(row.Status == "verified" && row.CharacterId != null && row.ProtectedProviderRefresh != null && row.SessionId == null,
                409, "login_not_ready", "Login is not ready or has already been redeemed.");
            if (!LoginPairingCode.Matches(body?.PairingCode, row.PairingCodeHash))
            {
                row.PairingFailures++;
                var locked = row.PairingFailures >= sessionPolicy.Value.MaxPairingFailures;
                if (locked) { row.Status = "failed"; row.FailureCode = "login_pairing_locked"; row.PairingCodeHash = null; row.ProtectedProviderRefresh = null; }
                // Persist failed guesses even though this response is a rejection.
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                throw new ApiFault(401, locked ? "login_pairing_locked" : "login_pairing_invalid", locked
                    ? "Too many incorrect pairing codes. Start login again."
                    : "Enter the pairing code shown in your verified browser page.");
            }
            var character = await db.Characters.SingleAsync(x => x.Id == row.CharacterId, ct);
            ApiFault.Require(await db.Sessions.CountAsync(x => x.CharacterId == character.Id && !x.Revoked && x.RefreshExpiresAt > time.GetUtcNow(), ct) < sessionPolicy.Value.MaxSessionsPerCharacter,
                409, "session_limit_reached", "Revoke an old device session before adding another.");
            var session = new SessionRow { CharacterId = character.Id, CreatedAt = time.GetUtcNow(), LastUsedAt = time.GetUtcNow(),
                ProtectedProviderRefresh = row.ProtectedProviderRefresh! };
            var tokens = Rotate(session, character); db.Sessions.Add(session); row.SessionId = session.Id; row.ProtectedProviderRefresh = null; row.PairingCodeHash = null;
            result = await commands.SaveAsync(http, op, new CommandResult<SessionTokens>(op.Id, tokens), ct);
        }
        await tx.CommitAsync(ct); return result;
    }
    private SessionTokens Rotate(SessionRow session, CharacterRow character)
    {
        var accessToken = SecretVault.NewSecret(); var refresh = SecretVault.NewSecret();
        session.AccessHash = SecretVault.Hash(accessToken); session.RefreshHash = SecretVault.Hash(refresh);
        session.AccessExpiresAt = time.GetUtcNow().AddMinutes(sessionPolicy.Value.AccessMinutes); session.RefreshExpiresAt = time.GetUtcNow().AddDays(sessionPolicy.Value.RefreshDays);
        session.LastUsedAt = time.GetUtcNow();
        return new(session.Id, SessionAccess.View(character), accessToken, session.AccessExpiresAt, refresh, session.RefreshExpiresAt);
    }
    public async Task<IResult> RefreshAsync(HttpContext http, RefreshRequest body, CancellationToken ct)
    {
        Enabled(); ApiFault.Require(SecretVault.IsCredential(body.RefreshToken), 401, "authentication_required", "Invalid renewal credential.");
        http.Response.Headers.CacheControl = "no-store";
        var hash = SecretVault.Hash(body.RefreshToken); var scope = "refresh:" + hash;
        // Resolve only the lock identity here. All authorization and replay checks
        // below run again under that session's lock, including after a rotation.
        var sessionId = await db.Sessions.Where(x => x.RefreshHash == hash).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? await db.UsedRefreshTokens.Where(x => x.TokenHash == hash).Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(ct);
        ApiFault.Require(sessionId.HasValue, 401, "authentication_required", "Sign in again.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["session:" + sessionId.GetValueOrDefault()], ct);
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        var prior = await db.UsedRefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (replay != null && prior != null)
        {
            ApiFault.Require(await db.Sessions.AnyAsync(x => x.Id == prior.SessionId && x.RefreshHash == prior.NextRefreshHash, ct),
                409, "refresh_superseded", "This renewal has already been superseded by another renewal.");
            ApiFault.Require(prior.ExpiresAt > time.GetUtcNow() && await db.Sessions.AnyAsync(x => x.Id == prior.SessionId && !x.Revoked && x.RefreshExpiresAt > time.GetUtcNow(), ct),
                401, "authentication_required", "Session was revoked or expired.");
            return commands.Replay(replay);
        }
        if (prior != null)
        {
            ApiFault.Require(prior.ExpiresAt > time.GetUtcNow(), 401, "authentication_required", "Renewal credential expired.");
            var stolen = await db.Sessions.SingleAsync(x => x.Id == prior.SessionId, ct); stolen.Revoked = true; stolen.ProtectedProviderRefresh = "";
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            throw new ApiFault(401, "refresh_reused", "Renewal credential was reused. Sign in again.");
        }
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.RefreshHash == hash, ct);
        ApiFault.Require(session != null && !session.Revoked && session.RefreshExpiresAt > time.GetUtcNow(), 401, "authentication_required", "Sign in again.");
        var character = await db.Characters.SingleAsync(x => x.Id == session!.CharacterId, ct);
        ProviderGrant? grant;
        try { grant = await provider.RenewAsync(vault.Unprotect(session!.ProtectedProviderRefresh, "provider"), character.LodestoneId, ct); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        { throw new ApiFault(503, "identity_provider_unavailable", "XIVAuth is unavailable. Your stored session is retained; retry renewal."); }
        if (grant == null || grant.Identity.LodestoneId != character.LodestoneId || SecretVault.Hash(grant.Identity.OwnershipKey) != character.OwnershipKeyHash)
        {
            session!.Revoked = true; session.ProtectedProviderRefresh = ""; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            throw new ApiFault(401, "identity_binding_changed", "Character ownership could not be reverified. Sign in again.");
        }
        if (character.HomeWorldName != grant.Identity.HomeWorldName) character.HomeWorldId = "";
        character.DisplayName = grant.Identity.DisplayName; character.HomeWorldName = grant.Identity.HomeWorldName; character.VerifiedAt = time.GetUtcNow();
        session!.ProtectedProviderRefresh = vault.Protect(grant.RefreshToken, "provider");
        var oldExpiry = session.RefreshExpiresAt;
        var op = commands.New(http, scope, body, character.Id, null, "session.refresh");
        var rotated = Rotate(session, character);
        db.UsedRefreshTokens.Add(new UsedRefreshRow { TokenHash = hash, SessionId = session.Id, NextRefreshHash = session.RefreshHash, ExpiresAt = oldExpiry });
        var result = await commands.SaveAsync(http, op, new CommandResult<SessionTokens>(op.Id, rotated), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> MeAsync(HttpContext http, CancellationToken ct)
    { var session = await access.GetAsync(http, ct); return Results.Json(SessionAccess.View(await db.Characters.SingleAsync(x => x.Id == session.CharacterId, ct))); }
    public async Task<IResult> SessionsAsync(HttpContext http, CancellationToken ct)
    {
        var current = await access.GetAsync(http, ct);
        var rows = await db.Sessions.Where(x => x.CharacterId == current.CharacterId && !x.Revoked && x.RefreshExpiresAt > time.GetUtcNow()).OrderBy(x => x.CreatedAt).ToArrayAsync(ct);
        return Results.Json(new SessionInfoPage(rows.Select(x => new SessionInfo(x.Id, x.CreatedAt, x.LastUsedAt, x.RefreshExpiresAt, x.Id == current.Id)).ToArray(), null, "1", null));
    }
    public async Task<IResult> RevokeAsync(HttpContext http, Guid? id, CancellationToken ct)
    {
        var current = await access.GetAsync(http, ct);
        var targetId = id ?? current.Id;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Never hold the global login lock while waiting for a renewal. Serialize
        // revocation receipts per actor and session mutations with RefreshAsync.
        await CommandStore.LockAsync(db, ["revocations:" + current.CharacterId, "session:" + targetId], ct);
        current = await access.GetAsync(http, ct);
        var scope = "character:" + current.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, null, ct); if (replay != null) return commands.Replay(replay);
        var target = await db.Sessions.SingleOrDefaultAsync(x => x.Id == targetId && x.CharacterId == current.CharacterId, ct);
        ApiFault.Require(target != null, 404, "not_found", "Session not found.");
        target!.Revoked = true; target.ProtectedProviderRefresh = "";
        var op = commands.New(http, scope, null, current.CharacterId, null, "session.revoke");
        var result = await commands.SaveAsync(http, op, new Receipt(op.Id), ct); await tx.CommitAsync(ct); return result;
    }
}
