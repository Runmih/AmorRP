using System.Net;
using System.Text;
using AmorRP.Plugin.Services.Api;
using Xunit;
namespace AmorRP.Plugin.Tests;

public sealed class FoundationRecoveryTests
{
    private static readonly Uri Origin = new("https://server.example/");
    private static readonly CommandRequest Use = new("POST", "api/v1/groups/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/inventory/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/use", "{\"expectedHoldingVersion\":1,\"chatConsent\":true,\"destination\":\"say\"}", null);
    [Fact]
    public async Task Lost_response_then_permission_rejection_retains_an_uncertain_outcome_and_same_key()
    {
        var handler = new Replies(denyAfterLoss: true); using var client = new FoundationClient(handler, TimeSpan.FromSeconds(10)); var key = Guid.CreateVersion7();
        var failure = await Assert.ThrowsAsync<FoundationApiException>(() => client.CommandWithMetadataAsync(Origin, Use, key, "test-token", TestContext.Current.CancellationToken));
        Assert.True(failure.OutcomeUncertain); Assert.Equal("safe-request-id", failure.RequestId); Assert.Equal("authentication_required", failure.Code);
        Assert.Equal(new[] { key.ToString(), key.ToString() }, handler.Keys); Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
    }
    [Fact]
    public async Task Lost_response_then_replayed_consumption_exposes_replay_without_changing_the_request()
    {
        var handler = new Replies(denyAfterLoss: false); using var client = new FoundationClient(handler, TimeSpan.FromSeconds(10)); var key = Guid.CreateVersion7();
        var response = await client.CommandWithMetadataAsync(Origin, Use, key, "test-token", TestContext.Current.CancellationToken);
        Assert.True(response.Replayed); Assert.Equal(new[] { key.ToString(), key.ToString() }, handler.Keys); Assert.Equal(handler.Bodies[0], handler.Bodies[1]);
    }
    [Fact]
    public async Task First_validation_rejection_is_definitive_and_exposes_only_safe_problem_fields()
    {
        var handler = new Replies(denyAfterLoss: true, loseFirst: false); using var client = new FoundationClient(handler, TimeSpan.FromSeconds(10));
        var failure = await Assert.ThrowsAsync<FoundationApiException>(() => client.CommandWithMetadataAsync(Origin, Use, Guid.CreateVersion7(), "test-token", TestContext.Current.CancellationToken));
        Assert.False(failure.OutcomeUncertain); Assert.Single(handler.Keys); Assert.Equal("safe-request-id", failure.RequestId);
    }
    private sealed class Replies(bool denyAfterLoss, bool loseFirst = true) : HttpMessageHandler
    {
        public List<string> Keys { get; } = [];
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Keys.Add(request.Headers.GetValues("Idempotency-Key").Single()); Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            if (loseFirst && Keys.Count == 1) throw new HttpRequestException("Response lost after an unknown server outcome.");
            if (denyAfterLoss) return new(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"code\":\"authentication_required\",\"title\":\"Reconnect to check the outcome.\",\"requestId\":\"safe-request-id\"}", Encoding.UTF8, "application/problem+json") };
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"operationId\":\"cccccccc-cccc-cccc-cccc-cccccccccccc\",\"result\":{}}", Encoding.UTF8, "application/json") };
            response.Headers.Add("Idempotency-Replayed", "true"); return response;
        }
    }
}
