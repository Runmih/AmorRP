using System.Net;
using System.Text;
using AmorRP.Contracts.Feasibility;
using AmorRP.Server.Features.Feasibility;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed class XivAuthProviderTests
{
    [Theory]
    [InlineData("character", true)]
    [InlineData("character:all", false)]
    public async Task Exchange_uses_fixed_server_endpoints_minimal_scope_and_selected_character_filter(string scope, bool accepted)
    {
        using var handler = new ProviderHandler(scope);
        using var client = new HttpClient(handler);
        var provider = new XivAuthProvider(client, Microsoft.Extensions.Options.Options.Create(new XivAuthOptions
        {
            ClientId = "test-id", ClientSecret = "test-secret", CallbackUrl = "https://example.com/auth/xivauth/feasibility-callback"
        }));
        if (accepted)
        {
            var identity = await provider.VerifyAsync("test-code", "test-verifier", new("Ada Example", "Raiden"), TestContext.Current.CancellationToken);
            Assert.Equal("ownership-binding", identity?.PersistentKey);
            Assert.Equal(2, handler.Calls);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.VerifyAsync("test-code", "test-verifier", new("Ada Example", "Raiden"), TestContext.Current.CancellationToken));
            Assert.Equal(1, handler.Calls);
        }
    }

    private sealed class ProviderHandler(string scope) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("https", request.RequestUri!.Scheme);
            Assert.Equal("xivauth.net", request.RequestUri.Host);
            if (Calls == 1)
            {
                Assert.Equal("/oauth/token", request.RequestUri.AbsolutePath);
                Assert.Equal(HttpMethod.Post, request.Method);
                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                Assert.Contains("code_verifier=test-verifier", form);
                Assert.Contains("client_secret=test-secret", form);
                Assert.DoesNotContain("refresh", form);
                return Json($$"""{"access_token":"test-provider-token","token_type":"Bearer","scope":"{{scope}}","expires_in":7200}""");
            }
            Assert.Equal("/api/v1/characters", request.RequestUri.AbsolutePath);
            Assert.Contains("home_world=Raiden", request.RequestUri.Query);
            Assert.Contains("name=Ada%20Example", request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-provider-token", request.Headers.Authorization?.Parameter);
            return Json("""[{"name":"Ada Example","home_world":"Raiden","persistent_key":"ownership-binding","lodestone_id":123456,"verified_at":"2026-10-01T00:00:00Z"}]""");
        }
        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
