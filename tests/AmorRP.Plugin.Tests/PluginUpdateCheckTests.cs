using System.Net;
using System.Text;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class PluginUpdateCheckTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly Version Installed = new(0, 4, 0, 10);

    [Theory]
    [InlineData("0.4.0.9", false)]
    [InlineData("0.4.0.10", false)]
    [InlineData("0.4.0.11", true)]
    public void Gameplay_waits_for_the_published_version(string version, bool updateRequired)
    {
        using var feed = new Feed(Release(version));
        using var check = new PluginUpdateCheck(Installed, feed);
        Assert.Equal(PluginUpdateStatus.Checking, check.Status);
        CompleteCheck(check, Now);
        Assert.Equal(updateRequired ? PluginUpdateStatus.Required : PluginUpdateStatus.Current, check.Status);
        Assert.Equal(Version.Parse(version), check.PublishedVersion);
    }

    [Fact]
    public void A_later_release_blocks_the_running_plugin_and_stays_required()
    {
        using var feed = new Feed(Release("0.4.0.10"), Release("0.4.0.11"));
        using var check = new PluginUpdateCheck(Installed, feed);
        CompleteCheck(check, Now);
        CompleteCheck(check, Now.AddMinutes(4));
        Assert.Equal(1, feed.Requests);
        CompleteCheck(check, Now.AddMinutes(5));
        Assert.Equal(PluginUpdateStatus.Required, check.Status);
        CompleteCheck(check, Now.AddHours(1));
        Assert.Equal(2, feed.Requests);
        Assert.Equal(PluginUpdateStatus.Required, check.Status);
    }

    [Fact]
    public void Verification_failure_pauses_gameplay_until_a_successful_retry()
    {
        using var feed = new Feed(Release("0.4.0.10"), null, Release("0.4.0.10"));
        using var check = new PluginUpdateCheck(Installed, feed);
        CompleteCheck(check, Now);
        Assert.Equal(PluginUpdateStatus.Current, check.Status);
        CompleteCheck(check, Now.AddMinutes(5));
        Assert.Equal(PluginUpdateStatus.Unavailable, check.Status);
        check.Retry();
        CompleteCheck(check, Now.AddMinutes(5));
        Assert.Equal(PluginUpdateStatus.Current, check.Status);
    }

    [Fact]
    public void A_manual_check_requests_a_fresh_feed_without_waiting_for_the_next_poll()
    {
        using var feed = new Feed(Release("0.4.0.10"), Release("0.4.0.11"));
        using var check = new PluginUpdateCheck(Installed, feed);
        CompleteCheck(check, Now);
        Assert.Equal(PluginUpdateStatus.Current, check.Status);
        check.Retry();
        CompleteCheck(check, Now.AddSeconds(1));
        Assert.Equal(PluginUpdateStatus.Required, check.Status);
        Assert.Equal(2, feed.Requests);
        Assert.All(feed.RequestUris, uri =>
        {
            Assert.Equal("raw.githubusercontent.com", uri.Host);
            Assert.Equal("/Runmih/AmorRP/main/repo.json", uri.AbsolutePath);
            Assert.StartsWith("?check=", uri.Query);
        });
        Assert.NotEqual(feed.RequestUris[0], feed.RequestUris[1]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("[{\"InternalName\":\"OtherPlugin\",\"AssemblyVersion\":\"0.4.0.10\"}]")]
    [InlineData("[{\"InternalName\":\"AmorRP\",\"AssemblyVersion\":\"0.4.0\"}]")]
    public void An_unverifiable_feed_cannot_unlock_an_unchecked_plugin(string? body)
    {
        using var feed = new Feed(body);
        using var check = new PluginUpdateCheck(Installed, feed);
        CompleteCheck(check, Now);
        Assert.Equal(PluginUpdateStatus.Unavailable, check.Status);
    }

    private static void CompleteCheck(PluginUpdateCheck check, DateTimeOffset time)
    {
        check.Tick(time);
        check.Tick(time);
    }

    private static string Release(string version) => $$"""[{"InternalName":"AmorRP","AssemblyVersion":"{{version}}"}]""";

    private sealed class Feed(params string?[] bodies) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];
        public int Requests => RequestUris.Count;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = bodies[Requests];
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(content == null ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
