using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using Xunit;
namespace AmorRP.Plugin.Tests;

public sealed class LoginStartTests
{
    [Theory]
    [InlineData("https://xivauth.net/oauth/authorize?state=test", true)]
    [InlineData("https://xivauth.net.evil.example/oauth/authorize", false)]
    public async Task Browser_login_only_returns_allowlisted_provider_urls_and_keeps_saved_credentials_out_of_start(string authorizationUrl, bool trusted)
    {
        using var handler = new LoginHandler(authorizationUrl);
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(1));
        var start = new LoginStart("Test Character", "35", "Zalera", Guid.NewGuid());
        if (trusted)
        {
            var result = await client.StartAsync(new Uri("https://server.example/"), start, TestContext.Current.CancellationToken);
            Assert.Equal(authorizationUrl, result.AuthorizationUrl);
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartAsync(new Uri("https://server.example/"), start, TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Requests);
    }
    [Fact]
    public async Task Old_server_cannot_silently_fall_back_to_unpaired_login()
    {
        using var handler = new LoginHandler("https://xivauth.net/oauth/authorize?state=test", pairing: false);
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.StartAsync(new Uri("https://server.example/"), new("Test", "35", "Zalera"), TestContext.Current.CancellationToken));
    }
    private sealed class LoginHandler(string authorizationUrl, bool pairing = true) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++; Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("/api/v2/auth/login-attempts", request.RequestUri!.AbsolutePath);
            Assert.Null(request.Headers.Authorization); Assert.True(request.Headers.Contains("Idempotency-Key")); Assert.True(request.Headers.Contains("X-Login-Request-Credential"));
            var json = await request.Content!.ReadAsStringAsync(ct); Assert.DoesNotContain("refreshToken", json); Assert.DoesNotContain("accessToken", json);
            var attempt = new LoginAttemptCreated(Guid.NewGuid(), authorizationUrl, new string('a', 43), DateTimeOffset.UtcNow.AddMinutes(5), 3, pairing);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CommandResult<LoginAttemptCreated>(Guid.NewGuid(), attempt)) };
        }
    }
}
