using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class BackendAddressTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5080/", true)]
    [InlineData("http://localhost:5080", true)]
    [InlineData("http://[::1]:5080", true)]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", false)]
    [InlineData("https://user:password@example.com", false)]
    [InlineData("https://example.com/?token=secret", false)]
    [InlineData("https://example.com/#fragment", false)]
    [InlineData("https://example.com/api/v1", false)]
    [InlineData("file:///etc/passwd", false)]
    public void Backend_origins_require_tls_except_for_deliberate_local_testing(string input, bool accepted)
    {
        Assert.Equal(accepted, BackendAddress.TryParse(input, out _));
    }
}
