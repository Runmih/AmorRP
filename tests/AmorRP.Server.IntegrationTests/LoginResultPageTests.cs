using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Fact]
    public async Task Browser_confirmation_keeps_secrets_out_and_plugin_can_still_exchange_after_replay_is_rejected()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient();
        using var start = Request(HttpMethod.Post, "/api/v2/auth/login-attempts", new LoginStart("Test character", "35", "Zalera", null));
        start.Headers.Add("X-Login-Request-Credential", SecretVault.NewSecret());
        using var started = await client.SendAsync(start, TestContext.Current.CancellationToken);
        var attempt = (await started.Content.ReadFromJsonAsync<CommandResult<LoginAttemptCreated>>(TestContext.Current.CancellationToken))!.Result;
        var state = QueryHelpers.ParseQuery(new Uri(attempt.AuthorizationUrl).Query)["state"].ToString();
        var callback = "/auth/xivauth/callback?state=" + state + "&code=180:private-owner-key";
        using var response = await client.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await CheckBrowserPage(response, "true");
        Assert.DoesNotContain(state, html);
        Assert.DoesNotContain("private-owner-key", html);
        Assert.DoesNotContain(attempt.AttemptCredential, html);
        Assert.DoesNotContain("https://", html); // No third-party assets or analytics on an OAuth result.

        var imagePath = Regex.Match(html, "/auth/assets/login-background-[a-f0-9]{12}\\.jpg").Value;
        Assert.NotEmpty(imagePath);
        using var image = await client.GetAsync(imagePath, TestContext.Current.CancellationToken);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType?.MediaType);
        Assert.True(image.Headers.CacheControl?.Public);
        var imageBytes = await image.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();
        Assert.Contains(hash[..12], imagePath);
        Assert.Equal($"\"{hash}\"", image.Headers.ETag?.Tag);

        using var repeated = await client.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        await CheckBrowserPage(repeated, "false");
        Assert.DoesNotContain("setInterval", html); // Pairing secret must not vanish on a timer.
        Assert.Contains("Never send them the code", html);
        using var exchange = Request(HttpMethod.Post, $"/api/v2/auth/login-attempts/{attempt.AttemptId}/exchange", new LoginExchangeRequest(PairingCodeFromPage(html)));
        exchange.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        using var exchanged = await client.SendAsync(exchange, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, exchanged.StatusCode);
        Assert.False(string.IsNullOrEmpty((await exchanged.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result.AccessToken));
    }

    [Fact]
    public async Task Cancelled_and_invalid_callbacks_show_a_failure_page_without_reflecting_provider_errors()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient();
        using var invalid = await client.GetAsync("/auth/xivauth/callback?state=invalid&error_description=%3Cscript%3Euntrusted%3C/script%3E", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.DoesNotContain("untrusted", await CheckBrowserPage(invalid, "false"));

        using var start = Request(HttpMethod.Post, "/api/v2/auth/login-attempts", new LoginStart("Test character", "35", "Zalera", null));
        start.Headers.Add("X-Login-Request-Credential", SecretVault.NewSecret());
        using var started = await client.SendAsync(start, TestContext.Current.CancellationToken);
        var attempt = (await started.Content.ReadFromJsonAsync<CommandResult<LoginAttemptCreated>>(TestContext.Current.CancellationToken))!.Result;
        var state = QueryHelpers.ParseQuery(new Uri(attempt.AuthorizationUrl).Query)["state"].ToString();
        using var denied = await client.GetAsync("/auth/xivauth/callback?state=" + state + "&error=access_denied&error_description=untrusted", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        Assert.DoesNotContain("untrusted", await CheckBrowserPage(denied, "false"));
        using var status = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/auth/login-attempts/{attempt.AttemptId}");
        status.Headers.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        using var statusResponse = await client.SendAsync(status, TestContext.Current.CancellationToken);
        Assert.Equal("failed", (await statusResponse.Content.ReadFromJsonAsync<LoginAttemptStatus>(TestContext.Current.CancellationToken))!.Status);
    }

    private static async Task<string> CheckBrowserPage(HttpResponseMessage response, string verified)
    {
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("no-referrer", response.Headers.GetValues("Referrer-Policy"));
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        var nonce = Regex.Match(csp, "script-src 'nonce-([^']+)'").Groups[1].Value;
        Assert.NotEmpty(nonce);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains($"<script nonce=\"{nonce}\">", html);
        Assert.Contains($"<style nonce=\"{nonce}\">", html);
        Assert.Contains($"data-verified=\"{verified}\"", html);
        Assert.DoesNotContain("{{", html);
        return html;
    }
}
