using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Features.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task One_Render_network_cannot_fill_the_global_pending_login_capacity()
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("RENDER", "true");
            b.UseSetting("SessionPolicy:MaxPendingLogins", "4");
            b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "2");
        });
        await Migrate(factory);
        using var attacker = factory.CreateClient();
        attacker.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.10");
        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.11");
        for (var i = 0; i < 2; i++)
        {
            using var started = await StartAdmissionLogin(attacker);
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        }
        using var blocked = await StartAdmissionLogin(attacker);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        using var legitimate = await StartAdmissionLogin(other);
        Assert.Equal(HttpStatusCode.Created, legitimate.StatusCode);
        using var fourth = await StartAdmissionLogin(other);
        Assert.Equal(HttpStatusCode.Created, fourth.StatusCode);
        using var third = factory.CreateClient();
        third.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.12");
        using var globallyFull = await StartAdmissionLogin(third);
        Assert.Equal(HttpStatusCode.TooManyRequests, globallyFull.StatusCode);
    }

    [Fact]
    public async Task Login_admission_replay_is_free_and_cancelled_attempts_still_count_toward_rolling_starts_across_restarts()
    {
        var key = Guid.CreateVersion7(clock.GetUtcNow()); var credential = SecretVault.NewSecret();
        using (var first = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "1");
            b.UseSetting("SessionPolicy:LoginStartsPerMinutePerNetwork", "2");
        }))
        {
            await Migrate(first); using var client = first.CreateClient();
            using var started = await StartAdmissionLogin(client, key, credential);
            var attempt = await AdmissionAttempt(started);
            using var replay = await StartAdmissionLogin(client, key, credential);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal(attempt.AttemptId, (await AdmissionAttempt(replay)).AttemptId);
            Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
            using var full = await StartAdmissionLogin(client);
            Assert.Equal(HttpStatusCode.TooManyRequests, full.StatusCode);
            await CancelAdmissionLogin(client, attempt);
            using var replacement = await StartAdmissionLogin(client);
            Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
            await CancelAdmissionLogin(client, await AdmissionAttempt(replacement));
        }
        using var restarted = Factory().WithWebHostBuilder(b => b.UseSetting("SessionPolicy:LoginStartsPerMinutePerNetwork", "2"));
        using var after = restarted.CreateClient();
        using var exhausted = await StartAdmissionLogin(after);
        Assert.Equal(HttpStatusCode.TooManyRequests, exhausted.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), exhausted.Headers.RetryAfter!.Delta);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        using var reset = await StartAdmissionLogin(after);
        Assert.Equal(HttpStatusCode.Created, reset.StatusCode);
    }

    [Fact]
    public async Task Browser_verified_logins_hold_their_slot_until_redeemed_or_expired_and_expired_origin_metadata_is_cleared()
    {
        using var factory = Factory().WithWebHostBuilder(b => b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "1"));
        await Migrate(factory); using var client = factory.CreateClient();
        var (attempt, code) = await PairingAttempt(client, "610");
        using var blocked = await StartAdmissionLogin(client);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        using var redeemed = await RedeemPairing(client, attempt, code);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        using var next = await StartAdmissionLogin(client);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        clock.UtcNow = clock.UtcNow.AddMinutes(11);
        using var expiredReplacement = await StartAdmissionLogin(client);
        Assert.Equal(HttpStatusCode.Created, expiredReplacement.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<LoginAdmission>().ClearExpiredOriginsAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Logins.Where(x => x.OriginHash != null).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Sessions.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("203.0.113.10", "::ffff:203.0.113.10")]
    [InlineData("2001:db8:42:1::1", "2001:db8:42:1::2")]
    public async Task Login_occupancy_uses_the_same_network_normalization_as_request_limits(string original, string alias)
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("RENDER", "true"); b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "1");
        });
        await Migrate(factory); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("CF-Connecting-IP", original);
        using var started = await StartAdmissionLogin(client);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        client.DefaultRequestHeaders.Remove("CF-Connecting-IP"); client.DefaultRequestHeaders.Add("CF-Connecting-IP", alias);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.99");
        client.DefaultRequestHeaders.Authorization = new("Bearer", SecretVault.NewSecret());
        using var blocked = await StartAdmissionLogin(client);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task Concurrent_starts_on_two_server_instances_cannot_overrun_network_or_global_capacity()
    {
        using var first = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("RENDER", "true"); b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "2"); b.UseSetting("SessionPolicy:MaxPendingLogins", "3");
        });
        using var second = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("RENDER", "true"); b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "2"); b.UseSetting("SessionPolicy:MaxPendingLogins", "3");
        });
        await Migrate(first); using var a = first.CreateClient(); using var b = second.CreateClient();
        a.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.10");
        b.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.10");
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => StartAdmissionLogin(i % 2 == 0 ? a : b)));
        Assert.Equal(2, results.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Equal(6, results.Count(x => x.StatusCode == HttpStatusCode.TooManyRequests));
        foreach (var response in results) response.Dispose();
        a.DefaultRequestHeaders.Remove("CF-Connecting-IP"); a.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.11");
        b.DefaultRequestHeaders.Remove("CF-Connecting-IP"); b.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.12");
        var global = await Task.WhenAll(StartAdmissionLogin(a), StartAdmissionLogin(b));
        Assert.Single(global, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(global, x => x.StatusCode == HttpStatusCode.TooManyRequests);
        foreach (var response in global) response.Dispose();
    }

    [Fact]
    public async Task Retained_vault_keys_preserve_admission_limits_during_key_rotation()
    {
        using (var first = Factory().WithWebHostBuilder(b => b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "1")))
        {
            await Migrate(first); using var client = first.CreateClient();
            using var started = await StartAdmissionLogin(client); Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        }
        using var rotated = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("SessionPolicy:MaxPendingLoginsPerNetwork", "1");
            b.UseSetting("SecretVault:ActiveKeyId", "new");
            b.UseSetting("SecretVault:Keys:new", Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()));
        });
        using var after = rotated.CreateClient(); using var rejected = await StartAdmissionLogin(after);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    private async Task CancelAdmissionLogin(HttpClient client, LoginAttemptCreated attempt)
    {
        using var request = Request(HttpMethod.Delete, $"/api/v2/auth/login-attempts/{attempt.AttemptId}");
        request.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_admission_upgrade_retires_unattributed_handoffs_and_preserves_saved_sessions()
    {
        using var factory = Factory(); await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>(); var ct = TestContext.Current.CancellationToken;
        await db.GetService<IMigrator>().MigrateAsync("20261015180055_LoginBrowserPairing", ct);
        var actor = Guid.NewGuid(); var session = Guid.NewGuid(); var pending = Guid.NewGuid(); var verified = Guid.NewGuid();
        var now = clock.GetUtcNow(); var expiry = now.AddMinutes(10);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO characters ("Id", "LodestoneId", "OwnershipKeyHash", "DisplayName", "HomeWorldId", "HomeWorldName", "VerifiedAt") VALUES ({actor}, '619', 'binding', 'Existing owner', '35', 'Zalera', {now});
            INSERT INTO sessions ("Id", "CharacterId", "AccessHash", "AccessExpiresAt", "RefreshHash", "RefreshExpiresAt", "ProtectedProviderRefresh", "CreatedAt", "LastUsedAt", "Revoked") VALUES ({session}, {actor}, 'access-hash', {expiry}, 'refresh-hash', {expiry}, 'saved-grant', {now}, {now}, false);
            INSERT INTO login_attempts ("Id", "CredentialHash", "StateHash", "ProtectedVerifier", "DisplayName", "HomeWorldId", "HomeWorldName", "Status", "ExpiresAt", "StateUsed", "ProtectedProviderRefresh", "CharacterId", "PairingCodeHash", "PairingFailures") VALUES
            ({pending}, 'attempt-a', 'state-a', 'pkce', 'Existing owner', '35', 'Zalera', 'pending', {expiry}, false, NULL, NULL, NULL, 0),
            ({verified}, 'attempt-b', 'state-b', '', 'Existing owner', '35', 'Zalera', 'verified', {expiry}, true, 'paired-grant', {actor}, 'pairing-hash', 0);
            """, ct);
        await db.Database.MigrateAsync(ct);
        var saved = await db.Sessions.SingleAsync(ct); Assert.Equal(session, saved.Id); Assert.False(saved.Revoked);
        Assert.Equal("refresh-hash", saved.RefreshHash); Assert.Equal("saved-grant", saved.ProtectedProviderRefresh);
        var attempts = await db.Logins.ToArrayAsync(ct); Assert.Equal(2, attempts.Length);
        Assert.All(attempts, row => { Assert.Equal("cancelled", row.Status); Assert.Null(row.OriginHash); Assert.Empty(row.ProtectedVerifier); Assert.Null(row.ProtectedProviderRefresh); Assert.Null(row.PairingCodeHash); });
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private async Task<HttpResponseMessage> StartAdmissionLogin(HttpClient client, Guid? key = null, string? credential = null)
    {
        using var request = Request(HttpMethod.Post, "/api/v2/auth/login-attempts", new LoginStart("Person", "35", "Zalera"), key);
        request.Headers.Add("X-Login-Request-Credential", credential ?? SecretVault.NewSecret());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<LoginAttemptCreated> AdmissionAttempt(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<CommandResult<LoginAttemptCreated>>(TestContext.Current.CancellationToken))!.Result;
}
