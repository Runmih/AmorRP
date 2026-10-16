using System.Net;
using System.Net.Http.Headers;
using AmorRP.Plugin.Services.Api;
using Xunit;
namespace AmorRP.Plugin.Tests;

public sealed class GroupImageDownloadTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Image_downloads_stop_at_the_budget_with_or_without_a_declared_length(bool declared)
    {
        var body = new CountingStream(new byte[4096]);
        using var handler = new ImageHandler(body, declared);
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetImageAsync(new("https://example.com/"), "api/v1/groups/id/logo", "token", 128, TestContext.Current.CancellationToken));
        Assert.True(body.BytesRead <= 129);
        Assert.Equal("Bearer token", handler.Authorization);
    }
    private sealed class ImageHandler(Stream body, bool declared) : HttpMessageHandler
    {
        public string? Authorization;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            var content = new StreamContent(body); content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            if (declared) content.Headers.ContentLength = 4096;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        public int BytesRead;
        public override bool CanSeek => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { var count = base.Read(buffer.Span); BytesRead += count; return ValueTask.FromResult(count); }
    }
}
