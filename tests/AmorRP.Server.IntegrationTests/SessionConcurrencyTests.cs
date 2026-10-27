using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    private Task<HttpResponseMessage> Renew(HttpClient client, SessionTokens tokens, Guid? key = null)
    {
        var request = Request(HttpMethod.Post, "/api/v2/auth/sessions/refresh", new RefreshRequest(tokens.RefreshToken), key);
        return Send();
        async Task<HttpResponseMessage> Send()
        { using (request) return await client.SendAsync(request, TestContext.Current.CancellationToken); }
    }

    private sealed class RenewalGate(string blockedCharacter)
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public async Task<ProviderGrant?> Renew(string refresh, string id, CancellationToken ct)
        {
            if (id == blockedCharacter)
            {
                Interlocked.Increment(ref Calls);
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
            return FakeDurableProvider.RenewalGrant(refresh, id);
        }
    }

    [Fact]
    public async Task A_slow_renewal_does_not_block_another_login_renewal_or_revocation()
    {
        using var factory = Factory(); await Migrate(factory);
        using var slow = factory.CreateClient(); var a = await Login(slow, "810");
        using var other = factory.CreateClient(); var b = await Login(other, "811");
        var gate = new RenewalGate("810"); provider.Renewal = gate.Renew;
        var pending = Renew(slow, a);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            using var newcomer = factory.CreateClient();
            await Login(newcomer, "812").WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            using var renewed = await Renew(other, b).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
            var tokens = (await renewed.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result;
            other.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
            using var revoke = Request(HttpMethod.Delete, "/api/v2/auth/sessions/current");
            using var revoked = await other.SendAsync(revoke, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
            Assert.False(pending.IsCompleted);
        }
        finally { gate.Release.TrySetResult(); }
        using var completed = await pending;
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Concurrent_renewals_exchange_once_and_preserve_replay_and_reuse_detection(bool sameKey)
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient();
        var original = await Login(client, "813"); var key = Guid.CreateVersion7();
        var gate = new RenewalGate("813"); provider.Renewal = gate.Renew;
        var first = Renew(client, original, key);
        Task<HttpResponseMessage>? second = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            second = Renew(client, original, sameKey ? key : Guid.CreateVersion7());
        }
        finally { gate.Release.TrySetResult(); }
        using var a = await first; using var b = await second!;
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(1, gate.Calls);
        var tokens = (await a.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result;
        if (sameKey)
        {
            Assert.Equal(HttpStatusCode.OK, b.StatusCode);
            Assert.True(b.Headers.Contains("Idempotency-Replayed"));
            Assert.Equal(tokens, (await b.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Unauthorized, b.StatusCode);
            Assert.Contains("refresh_reused", await b.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
            using var denied = await client.GetAsync("/api/v2/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
    }

    [Fact]
    public async Task Revoking_a_renewing_device_does_not_block_logins_or_leave_rotated_tokens_valid()
    {
        using var factory = Factory(); await Migrate(factory);
        using var device = factory.CreateClient(); var tokens = await Login(device, "814");
        using var adminDevice = factory.CreateClient(); await Login(adminDevice, "814");
        var gate = new RenewalGate("814"); provider.Renewal = gate.Renew;
        var renewal = Renew(device, tokens);
        Task<HttpResponseMessage>? revocation = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            using var request = Request(HttpMethod.Delete, "/api/v2/me/sessions/" + tokens.SessionId);
            revocation = adminDevice.SendAsync(request, TestContext.Current.CancellationToken);
            using var newcomer = factory.CreateClient();
            await Login(newcomer, "815").WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally { gate.Release.TrySetResult(); }
        using var renewed = await renewal;
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        using var revoked = await revocation!;
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var rotated = (await renewed.Content.ReadFromJsonAsync<CommandResult<SessionTokens>>(TestContext.Current.CancellationToken))!.Result;
        device.DefaultRequestHeaders.Authorization = new("Bearer", rotated.AccessToken);
        using var denied = await device.GetAsync("/api/v2/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Provider_failure_rolls_back_renewal_so_the_same_operation_can_be_retried()
    {
        using var factory = Factory(); await Migrate(factory); using var client = factory.CreateClient();
        var original = await Login(client, "816"); var key = Guid.CreateVersion7();
        provider.Renewal = (_, _, _) => throw new HttpRequestException("provider unavailable");
        using var failed = await Renew(client, original, key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        using var stillValid = await client.GetAsync("/api/v2/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
        provider.Renewal = null;
        using var retried = await Renew(client, original, key);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }
}
