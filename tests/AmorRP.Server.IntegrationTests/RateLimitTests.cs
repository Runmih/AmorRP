using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task One_network_cannot_exhaust_another_networks_api_or_login_budget(bool login)
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("ServicePolicy:ApiRequestsPerMinute", login ? "6000" : "60");
            b.UseSetting("ServicePolicy:LoginRequestsPerMinute", "60");
        });
        await Migrate(factory);
        var path = login ? "/auth/xivauth/callback?state=invalid" : "/api/v2/capabilities";
        var normal = login ? StatusCodes.Status400BadRequest : StatusCodes.Status200OK;
        for (var i = 0; i < 60; i++) Assert.Equal(normal, (await NetworkRequest(factory.Server, path, "203.0.113.10")).Response.StatusCode);
        var limited = await NetworkRequest(factory.Server, path, "203.0.113.10");
        Assert.Equal(StatusCodes.Status429TooManyRequests, limited.Response.StatusCode);
        Assert.Equal("60", limited.Response.Headers.RetryAfter.ToString());
        Assert.Equal(StatusCodes.Status200OK, (await NetworkRequest(factory.Server, "/health/live", "203.0.113.10")).Response.StatusCode);
        var other = await NetworkRequest(factory.Server, path, "203.0.113.11");
        Assert.Equal(normal, other.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Trusted_proxy_forwarding_separates_clients_but_does_not_trust_a_spoofed_prefix(bool network)
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("ServicePolicy:ApiRequestsPerMinute", "60");
            b.UseSetting(network ? "ReverseProxy:KnownNetworks:0" : "ReverseProxy:KnownProxies:0", network ? "10.20.0.0/24" : "10.20.0.1");
        });
        await Migrate(factory);
        for (var i = 0; i < 60; i++)
            Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", $"198.51.100.{i}, 203.0.113.10")).Response.StatusCode);
        Assert.Equal(429, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", "198.51.100.99, 203.0.113.10")).Response.StatusCode);
        Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", "198.51.100.99, 203.0.113.11")).Response.StatusCode);
    }

    [Theory]
    [InlineData("203.0.113.10", "::ffff:203.0.113.10", "203.0.113.11", true)]
    [InlineData("203.0.113.10", "::ffff:203.0.113.10", "203.0.113.11", false)]
    [InlineData("2001:db8:abcd:42::1", "2001:db8:abcd:42::2", "2001:db8:abcd:43::1", true)]
    public async Task Untrusted_headers_fake_bearers_and_address_aliases_do_not_create_new_budgets(string original, string alias, string other, bool configureProxy)
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("ServicePolicy:ApiRequestsPerMinute", "60");
            if (configureProxy) b.UseSetting("ReverseProxy:KnownProxies:0", "10.20.0.1");
        });
        await Migrate(factory);
        for (var i = 0; i < 60; i++)
            Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", original, $"198.51.100.{i}", "unverified-" + i)).Response.StatusCode);
        Assert.Equal(429, (await NetworkRequest(factory.Server, "/api/v2/capabilities", alias, "198.51.100.99", "new-unverified")).Response.StatusCode);
        Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", other, "198.51.100.99")).Response.StatusCode);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    public void Trusting_every_forwarding_source_is_rejected_at_startup(string network)
    {
        using var factory = Factory().WithWebHostBuilder(b => b.UseSetting("ReverseProxy:KnownNetworks:0", network));
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("KnownNetworks", error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_clients_have_separate_api_and_login_budgets_despite_shared_or_changing_proxy_addresses(bool login)
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("RENDER", "true");
            b.UseSetting("ServicePolicy:ApiRequestsPerMinute", login ? "6000" : "60");
            b.UseSetting("ServicePolicy:LoginRequestsPerMinute", "60");
        });
        await Migrate(factory);
        var path = login ? "/auth/xivauth/callback?state=invalid" : "/api/v2/capabilities";
        var normal = login ? 400 : 200;
        for (var i = 0; i < 60; i++)
            Assert.Equal(normal, (await NetworkRequest(factory.Server, path, $"10.20.0.{i + 1}",
                $"198.51.100.{i}, 203.0.113.10, 104.16.0.1", "fake-" + i, "203.0.113.10")).Response.StatusCode);
        Assert.Equal(429, (await NetworkRequest(factory.Server, path, "10.20.0.61", "1.2.3.4", renderIp: "::ffff:203.0.113.10")).Response.StatusCode);
        Assert.Equal(normal, (await NetworkRequest(factory.Server, path, "10.20.0.61", renderIp: "203.0.113.11")).Response.StatusCode);
        Assert.Equal(200, (await NetworkRequest(factory.Server, "/health/live", "10.20.0.61")).Response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("198.51.100.1, 198.51.100.2")]
    [InlineData("not-an-ip")]
    [InlineData("203.0.113.1:443")]
    [InlineData("[2001:db8::1]")]
    [InlineData("fe80::1%eth0")]
    [InlineData("2130706433")]
    [InlineData("203.0.113.01")]
    public async Task Render_missing_or_invalid_ingress_identity_is_rejected_without_shared_proxy_fallback(string? renderIp)
    {
        using var factory = Factory().WithWebHostBuilder(b => b.UseSetting("RENDER", "true"));
        await Migrate(factory);
        var response = await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", "203.0.113.99", renderIp: renderIp);
        Assert.Equal(503, response.Response.StatusCode);
        Assert.Equal("no-store", response.Response.Headers.CacheControl.ToString());
        Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", renderIp: "203.0.113.10")).Response.StatusCode);
    }

    [Fact]
    public async Task Render_ipv6_host_rotation_shares_a_budget_and_duplicate_headers_are_rejected()
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("RENDER", "true"); b.UseSetting("ServicePolicy:ApiRequestsPerMinute", "60");
        });
        await Migrate(factory);
        for (var i = 0; i < 60; i++)
            Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", renderIp: $"2001:db8:42:1::{i + 1:x}")).Response.StatusCode);
        Assert.Equal(429, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", renderIp: "2001:0db8:0042:0001:0000:0000:0000:0061")).Response.StatusCode);
        Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "10.20.0.1", renderIp: "2001:db8:42:2::1")).Response.StatusCode);
        var duplicate = await factory.Server.SendAsync(context =>
        {
            context.Request.Method = "GET"; context.Request.Path = "/api/v2/capabilities";
            context.Request.Headers.Append("CF-Connecting-IP", "203.0.113.1");
            context.Request.Headers.Append("CF-Connecting-IP", "203.0.113.2");
        }, TestContext.Current.CancellationToken);
        Assert.Equal(503, duplicate.Response.StatusCode);
    }

    [Fact]
    public async Task Non_Render_connections_ignore_forged_Cloudflare_headers()
    {
        using var factory = Factory().WithWebHostBuilder(b => b.UseSetting("ServicePolicy:ApiRequestsPerMinute", "60"));
        await Migrate(factory);
        for (var i = 0; i < 60; i++)
            Assert.Equal(200, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "203.0.113.10", renderIp: $"198.51.100.{i}")).Response.StatusCode);
        Assert.Equal(429, (await NetworkRequest(factory.Server, "/api/v2/capabilities", "203.0.113.10", renderIp: "198.51.100.99")).Response.StatusCode);
    }

    [Fact]
    public async Task Render_ingress_supports_the_complete_paired_login_and_authenticated_requests()
    {
        using var factory = Factory().WithWebHostBuilder(b => b.UseSetting("RENDER", "true"));
        await Migrate(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.10");
        await Login(client, "render-login");
        await Create(client);
    }

    [Fact]
    public void Render_and_generic_forwarding_configuration_cannot_be_combined()
    {
        using var factory = Factory().WithWebHostBuilder(b =>
        {
            b.UseSetting("RENDER", "true"); b.UseSetting("ReverseProxy:KnownProxies:0", "10.20.0.1");
        });
        Assert.Contains("cannot be combined", Assert.ThrowsAny<Exception>(() => factory.CreateClient()).ToString());
    }

    private static Task<HttpContext> NetworkRequest(Microsoft.AspNetCore.TestHost.TestServer server, string path, string remote, string? forwarded = null, string? bearer = null, string? renderIp = null)
        => server.SendAsync(context =>
        {
            var parts = path.Split('?', 2); context.Request.Method = "GET"; context.Request.Path = parts[0];
            if (parts.Length == 2) context.Request.QueryString = new QueryString("?" + parts[1]);
            context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            if (forwarded != null) { context.Request.Headers["X-Forwarded-For"] = forwarded; context.Request.Headers["X-Real-IP"] = forwarded; }
            if (bearer != null) context.Request.Headers.Authorization = "Bearer " + bearer;
            if (renderIp != null) context.Request.Headers["CF-Connecting-IP"] = renderIp;
        }, TestContext.Current.CancellationToken);
}
