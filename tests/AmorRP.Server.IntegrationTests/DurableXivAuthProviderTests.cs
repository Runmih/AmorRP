using System.Net;
using System.Text;
using AmorRP.Contracts.Auth;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Feasibility;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed class DurableXivAuthProviderTests
{
    [Fact]
    public async Task Renewal_uses_minimal_refresh_grant_and_verified_lodestone_resource_without_name_lookup()
    {
        using var handler = new DurableHandler(); using var http = new HttpClient(handler);
        var provider = new DurableXivAuthProvider(http, Microsoft.Extensions.Options.Options.Create(new XivAuthOptions {
            ClientId = "client", ClientSecret = "server-secret", DurableCallbackUrl = "https://rp.example/auth/xivauth/callback" }));
        var query = QueryHelpers.ParseQuery(new Uri(provider.AuthorizationUrl("state", "challenge")).Query);
        Assert.Equal("character refresh", query["scope"]); Assert.Equal("S256", query["code_challenge_method"]);
        var grant = await provider.RenewAsync("old-refresh", "123456", TestContext.Current.CancellationToken);
        Assert.Equal("123456", grant!.Identity.LodestoneId); Assert.Equal("verified-key", grant.Identity.OwnershipKey); Assert.Equal("rotated-refresh", grant.RefreshToken); Assert.Equal(2, handler.Calls);
    }
    private sealed class DurableHandler : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal("xivauth.net", request.RequestUri!.Host); Assert.Equal("https", request.RequestUri.Scheme);
            if (Calls == 1)
            {
                Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("/oauth/token", request.RequestUri.AbsolutePath);
                var form = await request.Content!.ReadAsStringAsync(ct); Assert.Contains("grant_type=refresh_token", form); Assert.Contains("refresh_token=old-refresh", form); Assert.Contains("client_secret=server-secret", form);
                return Json("""{"access_token":"provider-access","refresh_token":"rotated-refresh","token_type":"Bearer","scope":"refresh character"}""");
            }
            Assert.Equal("/api/v1/characters/123456", request.RequestUri.AbsolutePath); Assert.Equal("", request.RequestUri.Query);
            Assert.Equal("provider-access", request.Headers.Authorization?.Parameter);
            return Json("""{"name":"Test Person","home_world":"Zalera","lodestone_id":123456,"persistent_key":"verified-key","verified_at":"2026-10-01T00:00:00Z"}""");
        }
        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
