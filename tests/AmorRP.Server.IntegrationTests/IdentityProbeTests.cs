using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AmorRP.Contracts.Feasibility;
using AmorRP.Server.Features.Feasibility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed class IdentityProbeTests
{
    private static readonly IdentityProbeStart Selected = new("Ada Example", "Raiden");
    private static readonly VerifiedCharacter Character = new("ownership-binding", "123456", "Ada Example", "Raiden");

    [Fact]
    public async Task Disabled_login_has_no_bypass_and_does_not_break_liveness()
    {
        using var factory = Factory(false);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/feasibility/login-attempts", Selected, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("service_unavailable", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("requestId").GetString()));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/v1/auth/login-attempts", new AmorRP.Contracts.Auth.LoginStart("Ada Example", "35", "Raiden"), TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Malformed_future_and_stale_operation_keys_are_rejected_without_exception()
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        foreach (var key in new[] { "not-a-uuid", "ffffffff-ffff-7fff-bfff-ffffffffffff", Guid.CreateVersion7(DateTimeOffset.UtcNow.AddHours(-1)).ToString() })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/feasibility/login-attempts");
            request.Content = JsonContent.Create(Selected);
            request.Headers.Add("Idempotency-Key", key);
            request.Headers.Add("X-Login-Request-Credential", IdentityProbeStore.Secret());
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Http_flow_verifies_exchanges_reconnects_and_revokes_without_poll_secrets()
    {
        using var factory = Factory(true);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/feasibility/login-attempts");
        request.Content = JsonContent.Create(Selected);
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        request.Headers.Add("X-Login-Request-Credential", IdentityProbeStore.Secret());
        using var createdResponse = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var attempt = (await createdResponse.Content.ReadFromJsonAsync<IdentityProbeCreated>(TestContext.Current.CancellationToken))!;
        using var callback = await client.GetAsync("/auth/xivauth/feasibility-callback?code=test&state=" + State(attempt), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Contains("no-referrer", callback.Headers.GetValues("Referrer-Policy"));
        Assert.DoesNotContain("ownership-binding", await callback.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        client.DefaultRequestHeaders.Add("X-Login-Attempt-Credential", attempt.AttemptCredential);
        var status = await client.GetStringAsync($"/api/v1/feasibility/login-attempts/{attempt.AttemptId}", TestContext.Current.CancellationToken);
        Assert.Contains("verified", status);
        Assert.DoesNotContain("accessToken", status);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var exchange = await client.PostAsync($"/api/v1/feasibility/login-attempts/{attempt.AttemptId}/exchange", null, TestContext.Current.CancellationToken);
        var session = (await exchange.Content.ReadFromJsonAsync<IdentityProbeSession>(TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(Character, await client.GetFromJsonAsync<VerifiedCharacter>("/api/v1/feasibility/identity", TestContext.Current.CancellationToken));
        using var logout = await client.DeleteAsync("/api/v1/feasibility/session", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/feasibility/identity", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Replay_credentials_callback_and_exchange_are_bound_to_the_original_attempt()
    {
        var clock = new TestClock();
        var store = new IdentityProbeStore(clock);
        var provider = new TestProvider();
        var key = Guid.CreateVersion7();
        var credential = IdentityProbeStore.Secret();
        var attempt = store.Start(key, credential, Selected, provider)!;
        Assert.Equal(attempt, store.Start(key, credential, Selected, provider));
        Assert.Null(store.Start(key, IdentityProbeStore.Secret(), Selected, provider));
        Assert.Null(store.Start(key, credential, Selected with { HomeWorld = "Omega" }, provider));
        Assert.Null(store.Status(key, IdentityProbeStore.Secret()));
        Assert.Null(store.Exchange(key, attempt.AttemptCredential, Guid.CreateVersion7()));
        Assert.False(await store.CompleteAsync(IdentityProbeStore.Secret(), "code", provider, TestContext.Current.CancellationToken));
        Assert.True(await store.CompleteAsync(State(attempt), "code", provider, TestContext.Current.CancellationToken));
        Assert.False(await store.CompleteAsync(State(attempt), "code", provider, TestContext.Current.CancellationToken));
        Assert.Equal(1, provider.Calls);
        var exchangeKey = Guid.CreateVersion7();
        var session = store.Exchange(key, attempt.AttemptCredential, exchangeKey)!;
        Assert.Equal(session, store.Exchange(key, attempt.AttemptCredential, exchangeKey));
        Assert.Null(store.Exchange(key, attempt.AttemptCredential, Guid.CreateVersion7()));
        store.Logout(session.AccessToken);
        Assert.Null(store.Exchange(key, attempt.AttemptCredential, exchangeKey));
    }

    [Fact]
    public async Task Concurrent_callbacks_and_exchange_issue_only_one_session()
    {
        var store = new IdentityProbeStore(new TestClock());
        var provider = new TestProvider();
        var attempt = store.Start(Guid.CreateVersion7(), IdentityProbeStore.Secret(), Selected, provider)!;
        var callbacks = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.CompleteAsync(State(attempt), "code", provider, TestContext.Current.CancellationToken)));
        Assert.Single(callbacks, x => x);
        var key = Guid.CreateVersion7();
        var sessions = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => store.Exchange(attempt.AttemptId, attempt.AttemptCredential, key), TestContext.Current.CancellationToken)));
        Assert.Single(sessions.Select(x => x!.AccessToken).Distinct());
    }

    [Fact]
    public async Task Expiry_restart_and_provider_failure_fail_closed()
    {
        var clock = new TestClock();
        var store = new IdentityProbeStore(clock);
        var provider = new TestProvider();
        var attempt = store.Start(Guid.CreateVersion7(), IdentityProbeStore.Secret(), Selected, provider)!;
        Assert.True(await store.CompleteAsync(State(attempt), "code", provider, TestContext.Current.CancellationToken));
        var session = store.Exchange(attempt.AttemptId, attempt.AttemptCredential, Guid.CreateVersion7())!;
        Assert.Null(new IdentityProbeStore(clock).Identity(session.AccessToken));
        clock.Now = clock.Now.AddMinutes(6);
        Assert.Null(store.Identity(session.AccessToken));
        clock.Now = clock.Now.AddMinutes(5);
        Assert.Equal("expired", store.Status(attempt.AttemptId, attempt.AttemptCredential)!.Status);
        Assert.False(await store.CompleteAsync(State(attempt), "code", provider, TestContext.Current.CancellationToken));
        var failed = store.Start(Guid.CreateVersion7(), IdentityProbeStore.Secret(), Selected, provider)!;
        provider.Unavailable = true;
        Assert.False(await store.CompleteAsync(State(failed), "code", provider, TestContext.Current.CancellationToken));
        Assert.Equal("provider_unavailable", store.Status(failed.AttemptId, failed.AttemptCredential)!.FailureCode);
        Assert.Null(store.Exchange(failed.AttemptId, failed.AttemptCredential, Guid.CreateVersion7()));
    }

    [Theory]
    [InlineData("Ada Example", "Raiden", "2026-10-01T00:00:00Z", true)]
    [InlineData("Other Character", "Raiden", "2026-10-01T00:00:00Z", false)]
    [InlineData("Ada Example", "Omega", "2026-10-01T00:00:00Z", false)]
    [InlineData("Ada Example", "Raiden", null, false)]
    public void Provider_fixture_rejects_wrong_name_world_and_unverified_character(string name, string world, string? verified, bool accepted)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new[] { new { persistent_key = "ownership-binding", lodestone_id = 123456, name, home_world = world, verified_at = verified } }));
        Assert.Equal(accepted, XivAuthProvider.ParseCharacter(json.RootElement, Selected) is not null);
    }

    [Fact]
    public void Authorization_is_pkce_bound_and_only_requests_character_scope()
    {
        var provider = new XivAuthProvider(new HttpClient(), Microsoft.Extensions.Options.Options.Create(new XivAuthOptions { ClientId = "public-id", CallbackUrl = "https://example.com/auth/xivauth/feasibility-callback" }));
        var url = new Uri(provider.AuthorizationUrl("state", "challenge"));
        var query = QueryHelpers.ParseQuery(url.Query);
        Assert.Equal("xivauth.net", url.Host);
        Assert.Equal("character", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("challenge", query["code_challenge"]);
        Assert.DoesNotContain("client_secret", query.Keys);
    }

    [Fact]
    public void Enabled_provider_requires_exact_https_callback_and_server_credentials()
    {
        Assert.True(new XivAuthOptions().IsValid());
        Assert.False(new XivAuthOptions { Enabled = true }.IsValid());
        var options = new XivAuthOptions { Enabled = true, ClientId = "id", ClientSecret = "secret", CallbackUrl = "https://example.com/auth/xivauth/feasibility-callback" };
        Assert.True(options.IsValid());
        options.CallbackUrl = "http://localhost/auth/xivauth/feasibility-callback";
        Assert.False(options.IsValid());
    }

    private static string State(IdentityProbeCreated attempt) => QueryHelpers.ParseQuery(new Uri(attempt.AuthorizationUrl).Query)["state"].ToString();
    private static WebApplicationFactory<Program> Factory(bool enabled) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", "Host=127.0.0.1;Port=1;Database=amorrp_test_probe;Username=test");
        builder.UseSetting("XivAuth:Enabled", enabled.ToString());
        builder.UseSetting("XivAuth:ClientId", "test-client");
        builder.UseSetting("XivAuth:ClientSecret", "test-secret");
        builder.UseSetting("XivAuth:CallbackUrl", "https://example.com/auth/xivauth/feasibility-callback");
        builder.ConfigureServices(services => { services.RemoveAll<ICharacterIdentityProvider>(); services.AddSingleton<ICharacterIdentityProvider, TestProvider>(); });
    });
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TestProvider : ICharacterIdentityProvider
    {
        public int Calls { get; private set; }
        public bool Unavailable { get; set; }
        public string AuthorizationUrl(string state, string challenge) => "https://xivauth.net/oauth/authorize?state=" + state;
        public Task<VerifiedCharacter?> VerifyAsync(string code, string verifier, IdentityProbeStart selected, CancellationToken cancellationToken)
        {
            Calls++;
            if (Unavailable) throw new HttpRequestException("Simulated provider outage; not exposed.");
            return Task.FromResult<VerifiedCharacter?>(Character);
        }
    }
}
