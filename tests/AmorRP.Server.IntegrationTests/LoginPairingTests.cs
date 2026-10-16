using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.RegularExpressions;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Shared_link_approval_does_not_let_the_initiator_redeem_without_the_browser_pairing_code()
    {
        using var factory = Factory(); await Migrate(factory);
        using var sender = factory.CreateClient(); using var browser = factory.CreateClient();
        using var start = Request(HttpMethod.Post, "/api/v1/auth/login-attempts", new LoginStart("Victim character", "35", "Zalera"));
        start.Headers.Add("X-Login-Request-Credential", SecretVault.NewSecret());
        using var started = await sender.SendAsync(start, TestContext.Current.CancellationToken);
        var attempt = (await started.Content.ReadFromJsonAsync<CommandResult<LoginAttemptCreated>>(TestContext.Current.CancellationToken))!.Result;
        var state = QueryHelpers.ParseQuery(new Uri(attempt.AuthorizationUrl).Query)["state"].ToString();
        using var approved = await browser.GetAsync("/auth/xivauth/callback?state=" + state + "&code=190:owner", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var browserCode = PairingCodeFromPage(await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(attempt.PairingRequired);
        using var poll = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/login-attempts/{attempt.AttemptId}");
        poll.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        using var polled = await sender.SendAsync(poll, TestContext.Current.CancellationToken);
        var publicStatus = await polled.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(browserCode, publicStatus);
        Assert.DoesNotContain("pairingCode", publicStatus);

        using var legacy = Request(HttpMethod.Post, $"/api/v1/auth/login-attempts/{attempt.AttemptId}/exchange");
        legacy.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        using var codeFree = await sender.SendAsync(legacy, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, codeFree.StatusCode);

        using var exchange = Request(HttpMethod.Post, $"/api/v1/auth/login-attempts/{attempt.AttemptId}/exchange", new { pairingCode = "0000000000000000" });
        exchange.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        using var stolen = await sender.SendAsync(exchange, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, stolen.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Empty(await db.Sessions.ToArrayAsync(TestContext.Current.CancellationToken));
        var stored = await db.Logins.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(browserCode, stored.PairingCodeHash);
        Assert.Equal(SecretVault.Hash(browserCode), stored.PairingCodeHash);
    }

    private static string PairingCodeFromPage(string html)
    {
        var match = Regex.Match(html, "<code id=\"pairing-code\">([A-F0-9]{16})</code>");
        Assert.True(match.Success, "Verified page must contain the browser-only pairing code.");
        return match.Groups[1].Value;
    }

    private async Task<(LoginAttemptCreated Attempt, string Code)> PairingAttempt(HttpClient client, string id)
    {
        using var start = Request(HttpMethod.Post, "/api/v1/auth/login-attempts", new LoginStart("Person " + id, "35", "Zalera"));
        start.Headers.Add("X-Login-Request-Credential", SecretVault.NewSecret());
        using var started = await client.SendAsync(start, TestContext.Current.CancellationToken);
        var attempt = (await started.Content.ReadFromJsonAsync<CommandResult<LoginAttemptCreated>>(TestContext.Current.CancellationToken))!.Result;
        var state = QueryHelpers.ParseQuery(new Uri(attempt.AuthorizationUrl).Query)["state"];
        using var browser = await client.GetAsync("/auth/xivauth/callback?state=" + state + "&code=" + id + ":owner", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, browser.StatusCode);
        return (attempt, PairingCodeFromPage(await browser.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    private async Task<HttpResponseMessage> RedeemPairing(HttpClient client, LoginAttemptCreated attempt, string code, Guid? key = null)
    {
        using var request = Request(HttpMethod.Post, $"/api/v1/auth/login-attempts/{attempt.AttemptId}/exchange", new LoginExchangeRequest(code), key);
        request.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Pairing_is_bound_to_one_attempt_and_guess_lockout_survives_restart()
    {
        LoginAttemptCreated attempt; string code;
        using (var first = Factory())
        {
            await Migrate(first); using var client = first.CreateClient();
            (attempt, code) = await PairingAttempt(client, "191");
            var other = await PairingAttempt(client, "192");
            using var crossed = await RedeemPairing(client, attempt, other.Code);
            Assert.Equal(HttpStatusCode.Unauthorized, crossed.StatusCode);
        }
        using var restarted = Factory(); using var sender = restarted.CreateClient();
        for (var i = 1; i < 5; i++)
        {
            using var guess = await RedeemPairing(sender, attempt, "wrong-code");
            Assert.Equal(HttpStatusCode.Unauthorized, guess.StatusCode);
        }
        using var locked = await RedeemPairing(sender, attempt, code);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        await using var scope = restarted.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        var row = await db.Logins.SingleAsync(x => x.Id == attempt.AttemptId, TestContext.Current.CancellationToken);
        Assert.Equal(5, row.PairingFailures); Assert.Equal("login_pairing_locked", row.FailureCode);
        Assert.Null(row.PairingCodeHash); Assert.Null(row.ProtectedProviderRefresh);
        Assert.Empty(await db.Sessions.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Correct_pairing_has_safe_same_key_retry_but_cannot_be_reused_or_replayed_with_a_different_code()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient();
        var (attempt, code) = await PairingAttempt(client, "193"); var key = Guid.CreateVersion7();
        using var first = await RedeemPairing(client, attempt, code, key); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var tokens = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var retry = await RedeemPairing(client, attempt, code, key); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True(retry.Headers.Contains("Idempotency-Replayed")); Assert.Equal(tokens, await retry.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var tampered = await RedeemPairing(client, attempt, "wrong-code", key); Assert.Equal(HttpStatusCode.Conflict, tampered.StatusCode);
        using var reused = await RedeemPairing(client, attempt, code); Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Single(await db.Sessions.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Null((await db.Logins.SingleAsync(TestContext.Current.CancellationToken)).PairingCodeHash);
    }

    [Fact]
    public async Task Cancelled_and_expired_pairing_attempts_cannot_create_sessions()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient();
        var cancelled = await PairingAttempt(client, "194"); var expired = await PairingAttempt(client, "195");
        using var cancel = Request(HttpMethod.Delete, $"/api/v1/auth/login-attempts/{cancelled.Attempt.AttemptId}");
        cancel.Headers.Add("X-Login-Attempt-Credential", cancelled.Attempt.AttemptCredential);
        using var response = await client.SendAsync(cancel, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var cancelledExchange = await RedeemPairing(client, cancelled.Attempt, cancelled.Code); Assert.Equal(HttpStatusCode.Conflict, cancelledExchange.StatusCode);
        clock.UtcNow = expired.Attempt.ExpiresAt.AddSeconds(1);
        using var expiredExchange = await RedeemPairing(client, expired.Attempt, expired.Code); Assert.Equal(HttpStatusCode.Conflict, expiredExchange.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AmorDbContext>();
        Assert.Empty(await db.Sessions.ToArrayAsync(TestContext.Current.CancellationToken));
    }
}
