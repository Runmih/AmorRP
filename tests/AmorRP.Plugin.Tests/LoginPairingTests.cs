using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class LoginPairingTests
{
    [Fact]
    public async Task Approval_only_polls_and_explicit_pairing_retries_the_same_code_and_key_after_a_lost_response()
    {
        var attempt = new LoginAttemptCreated(Guid.NewGuid(), "https://xivauth.net/oauth/authorize", "attempt-secret", DateTimeOffset.UtcNow.AddMinutes(5), 3, true);
        var key = Guid.CreateVersion7(); using var handler = new PairingHandler(attempt, key);
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(1)); var origin = new Uri("https://server.example/");
        await client.WaitForVerificationAsync(origin, attempt, TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Polls); Assert.Equal(0, handler.Exchanges);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeLoginAsync(origin, attempt, "", key, TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Exchanges);
        var tokens = await client.ExchangeLoginAsync(origin, attempt, "1234567890ABCDEF", key, TestContext.Current.CancellationToken);
        Assert.Equal("paired-access", tokens.AccessToken); Assert.Equal(2, handler.Exchanges);
    }

    private sealed class PairingHandler(LoginAttemptCreated attempt, Guid key) : HttpMessageHandler
    {
        public int Polls, Exchanges;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Null(request.Headers.Authorization);
            Assert.Equal(attempt.AttemptCredential, Assert.Single(request.Headers.GetValues("X-Login-Attempt-Credential")));
            Assert.DoesNotContain("1234567890ABCDEF", request.RequestUri!.ToString());
            if (request.Method == HttpMethod.Get)
            {
                Polls++;
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new LoginAttemptStatus(attempt.AttemptId, "verified", attempt.ExpiresAt, null)) };
            }
            Assert.Equal(HttpMethod.Post, request.Method); Assert.EndsWith("/exchange", request.RequestUri.AbsolutePath);
            Assert.Equal(key.ToString(), Assert.Single(request.Headers.GetValues("Idempotency-Key")));
            Assert.Equal("1234567890ABCDEF", (await request.Content!.ReadFromJsonAsync<LoginExchangeRequest>(ct))!.PairingCode);
            Exchanges++; if (Exchanges == 1) throw new HttpRequestException("Simulated lost exchange response");
            var now = DateTimeOffset.UtcNow;
            var tokens = new SessionTokens(Guid.NewGuid(), new(Guid.NewGuid(), "Character", "35", "Zalera", now, "verified"), "paired-access", now.AddMinutes(30), "paired-refresh", now.AddDays(30));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CommandResult<SessionTokens>(Guid.NewGuid(), tokens)) };
        }
    }
}
