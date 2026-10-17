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
    [Theory]
    [InlineData("renew")]
    [InlineData("login-new")]
    [InlineData("login-known")]
    public async Task Grants_use_XIVAuth_v1_character_resources_independently_of_AmorRP_API_version(string flow)
    {
        using var handler = new DurableHandler(flow); using var http = new HttpClient(handler);
        var provider = new DurableXivAuthProvider(http, Microsoft.Extensions.Options.Options.Create(new XivAuthOptions {
            ClientId = "client", ClientSecret = "server-secret", DurableCallbackUrl = "https://rp.example/auth/xivauth/callback" }));
        var query = QueryHelpers.ParseQuery(new Uri(provider.AuthorizationUrl("state", "challenge")).Query);
        Assert.Equal("character refresh", query["scope"]); Assert.Equal("S256", query["code_challenge_method"]);
        var grant = flow == "renew"
            ? await provider.RenewAsync("old-refresh", "123456", TestContext.Current.CancellationToken)
            : await provider.ExchangeAsync("login-code", "pkce-verifier", new LoginStart("Test Person", "41", "Zalera"),
                flow == "login-known" ? "123456" : null, TestContext.Current.CancellationToken);
        Assert.Equal("123456", grant!.Identity.LodestoneId); Assert.Equal("verified-key", grant.Identity.OwnershipKey); Assert.Equal("rotated-refresh", grant.RefreshToken); Assert.Equal(2, handler.Calls);
    }
    // These paths are the external provider contract, not Amor RP API routes.
    private sealed class DurableHandler(string flow) : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal("xivauth.net", request.RequestUri!.Host); Assert.Equal("https", request.RequestUri.Scheme);
            if (Calls == 1)
            {
                Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("/oauth/token", request.RequestUri.AbsolutePath);
                var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal("client", form["client_id"]); Assert.Equal("server-secret", form["client_secret"]);
                if (flow == "renew")
                {
                    Assert.Equal("refresh_token", form["grant_type"]); Assert.Equal("old-refresh", form["refresh_token"]);
                    Assert.False(form.ContainsKey("code"));
                }
                else
                {
                    Assert.Equal("authorization_code", form["grant_type"]); Assert.Equal("login-code", form["code"]);
                    Assert.Equal("pkce-verifier", form["code_verifier"]);
                    Assert.Equal("https://rp.example/auth/xivauth/callback", form["redirect_uri"]);
                }
                return Json("""{"access_token":"provider-access","refresh_token":"rotated-refresh","token_type":"Bearer","scope":"refresh character"}""");
            }
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(flow == "login-new" ? "/api/v1/characters" : "/api/v1/characters/123456", request.RequestUri.AbsolutePath);
            if (flow == "login-new")
            {
                var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
                Assert.Equal("Test Person", query["name"]); Assert.Equal("Zalera", query["home_world"]);
            }
            else Assert.Equal("", request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("provider-access", request.Headers.Authorization?.Parameter);
            const string character = """{"name":"Test Person","home_world":"Zalera","lodestone_id":123456,"persistent_key":"verified-key","verified_at":"2026-10-01T00:00:00Z"}""";
            return Json(flow == "login-new" ? "[" + character + "]" : character);
        }
        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
