using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AmorRP.Contracts.Feasibility;
using Microsoft.AspNetCore.WebUtilities;

namespace AmorRP.Server.Features.Feasibility;

// Bounded, single-instance, short-lived M1 spike. Never used by production auth routes.
public sealed class IdentityProbeStore(TimeProvider clock)
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Attempt> attempts = [];
    private readonly Dictionary<string, IdentityProbeSession> sessions = [];
    private const int Capacity = 256;

    public static string Secret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string Digest(string value) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool ValidSecret(string value) => value.Length == 43 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public IdentityProbeCreated? Start(Guid key, string credential, IdentityProbeStart selected, ICharacterIdentityProvider provider)
    {
        lock (gate)
        {
            Cleanup();
            if (attempts.TryGetValue(key, out var old))
                return old.RequestCredentialHash == Digest(credential) && old.Selected == selected ? old.Created : null;
            if (attempts.Count >= Capacity) return null;
            var state = Secret();
            var verifier = Secret();
            var created = new IdentityProbeCreated(key, provider.AuthorizationUrl(state, Digest(verifier)), Secret(), clock.GetUtcNow().AddMinutes(10), 3);
            attempts.Add(key, new Attempt(created, selected, Digest(credential), Digest(state), verifier));
            return created;
        }
    }

    public IdentityProbeStatus? Status(Guid id, string credential)
    {
        lock (gate)
        {
            if (!Find(id, credential, out var attempt)) return null;
            return new(id, attempt!.Created.ExpiresAt <= clock.GetUtcNow() ? "expired" : attempt.Status == "verifying" ? "pending" : attempt.Status,
                attempt.Created.ExpiresAt, attempt.FailureCode);
        }
    }

    public async Task<bool> CompleteAsync(string state, string? code, ICharacterIdentityProvider provider, CancellationToken cancellationToken)
    {
        Attempt? attempt;
        lock (gate)
        {
            attempt = attempts.Values.SingleOrDefault(a => a.StateHash == Digest(state));
            if (attempt is null || attempt.Status != "pending" || attempt.Created.ExpiresAt <= clock.GetUtcNow()) return false;
            // Consume the state before making any asynchronous provider request.
            attempt.Status = "verifying";
        }
        VerifiedCharacter? verified = null;
        var failure = "wrong_or_unverified_character";
        try
        {
            if (!string.IsNullOrEmpty(code)) verified = await provider.VerifyAsync(code, attempt.Verifier, attempt.Selected, cancellationToken);
            else failure = "consent_denied";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
        {
            failure = "provider_unavailable";
        }
        lock (gate)
        {
            if (attempt.Created.ExpiresAt <= clock.GetUtcNow()) return false;
            attempt.Character = verified;
            attempt.Status = verified is null ? "failed" : "verified";
            attempt.FailureCode = verified is null ? failure : null;
            return verified is not null;
        }
    }

    public IdentityProbeSession? Exchange(Guid id, string credential, Guid key)
    {
        lock (gate)
        {
            Cleanup();
            if (!Find(id, credential, out var attempt) || attempt!.Created.ExpiresAt <= clock.GetUtcNow()
                || attempt.Status != "verified" || attempt.Character is null) return null;
            if (attempt.Session is not null)
                return attempt.ExchangeKey == key && attempt.Session.ExpiresAt > clock.GetUtcNow()
                    && sessions.ContainsKey(Digest(attempt.Session.AccessToken)) ? attempt.Session : null;
            if (sessions.Count >= Capacity) return null;
            attempt.Session = new(Secret(), clock.GetUtcNow().AddMinutes(5), attempt.Character);
            attempt.ExchangeKey = key;
            sessions.Add(Digest(attempt.Session.AccessToken), attempt.Session);
            return attempt.Session;
        }
    }

    public VerifiedCharacter? Identity(string token)
    {
        lock (gate)
        {
            Cleanup();
            return sessions.TryGetValue(Digest(token), out var session) ? session.Character : null;
        }
    }

    public void Logout(string token) { lock (gate) sessions.Remove(Digest(token)); }

    private bool Find(Guid id, string credential, out Attempt? attempt) =>
        attempts.TryGetValue(id, out attempt) && ValidSecret(credential) && Digest(credential) == Digest(attempt.Created.AttemptCredential);

    private void Cleanup()
    {
        var now = clock.GetUtcNow();
        // Retain expired attempts briefly so a retry never silently starts a new login.
        foreach (var id in attempts.Where(x => x.Value.Created.ExpiresAt.AddMinutes(10) <= now).Select(x => x.Key).ToArray()) attempts.Remove(id);
        foreach (var key in sessions.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray()) sessions.Remove(key);
    }

    private sealed class Attempt(IdentityProbeCreated created, IdentityProbeStart selected, string requestCredentialHash, string stateHash, string verifier)
    {
        public IdentityProbeCreated Created { get; } = created;
        public IdentityProbeStart Selected { get; } = selected;
        public string RequestCredentialHash { get; } = requestCredentialHash;
        public string StateHash { get; } = stateHash;
        public string Verifier { get; } = verifier;
        public string Status { get; set; } = "pending";
        public string? FailureCode { get; set; }
        public VerifiedCharacter? Character { get; set; }
        public Guid? ExchangeKey { get; set; }
        public IdentityProbeSession? Session { get; set; }
    }
}
