using System.Net;
using System.Text;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class BackendProbeTests
{
    [Theory]
    [InlineData("1", "0.0.1", 200, true)]
    [InlineData("2", "0.0.1", 200, false)]
    [InlineData("1", "99.0.0", 200, false)]
    [InlineData("1", "not-a-version", 200, false)]
    [InlineData("1", "0.0.1", 503, false)]
    public async Task Probe_checks_protocol_minimum_client_and_database_readiness(string api, string minimum, int readyStatus, bool expected)
    {
        using var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/health/live" => Json(200, "{\"status\":\"ok\"}"),
            "/api/v2/capabilities" => Json(200, $$"""{"apiVersion":"{{api}}","minimumClientVersion":"{{minimum}}","serverVersion":"0.0.6.0"}"""),
            "/health/ready" => Json(readyStatus, readyStatus == 200 ? "{\"status\":\"ok\"}" : "{\"status\":\"unavailable\"}"),
            _ => throw new InvalidOperationException("Unexpected request")
        });
        using var probe = new BackendProbe(handler, TimeSpan.FromSeconds(1));
        var result = await probe.CheckAsync(new Uri("https://example.com"), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.IsReady);
        Assert.DoesNotContain("password", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(200, "<html>not our server</html>")]
    [InlineData(302, "redirect")]
    public async Task Unexpected_content_and_redirects_fail_without_a_second_request(int status, string content)
    {
        using var handler = new StubHandler(_ => Json(status, content));
        using var probe = new BackendProbe(handler, TimeSpan.FromSeconds(1));
        Assert.False((await probe.CheckAsync(new Uri("https://example.com"), TestContext.Current.CancellationToken)).IsReady);
        Assert.Equal(1, handler.Requests);
    }


    [Theory]
    [InlineData("0.0.4", false)]
    [InlineData("0.0.5.1", false)]
    [InlineData("0.0.5.2", false)]
    [InlineData("0.0.6.0", true)]
    [InlineData("invalid", false)]
    public async Task M5_discovery_requires_the_server_upgrade_before_readiness(string serverVersion, bool expected)
    {
        var readinessRequests = 0;
        using var handler = new StubHandler(request => {
            if (request.RequestUri!.AbsolutePath == "/api/v2/capabilities")
                return Json(200, $$"""{"apiVersion":"1","minimumClientVersion":"0.0.5","serverVersion":"{{serverVersion}}","maintenance":false}""");
            if (request.RequestUri.AbsolutePath == "/health/ready") readinessRequests++;
            return Json(200, "{\"status\":\"ok\"}");
        });
        using var probe = new BackendProbe(handler, TimeSpan.FromSeconds(1));
        var result = await probe.CheckAsync(new Uri("https://example.com"), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.IsReady); Assert.Equal(expected ? 1 : 0, readinessRequests);
        if (!expected) Assert.Contains("Upgrade the server", result.Summary);
    }

    private static HttpResponseMessage Json(int status, string json) => new((HttpStatusCode)status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(response(request));
        }
    }
}
