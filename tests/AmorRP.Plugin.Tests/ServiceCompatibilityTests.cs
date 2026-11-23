using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class ServiceCompatibilityTests
{
    [Theory]
    [InlineData("2", "0.11.0.0", "0.4.0.3", true)]
    [InlineData("2", "0.7.0.0", "0.4.0.3", false)]
    [InlineData("2", "0.11.0.1", "0.5.0.0", true)]
    [InlineData("2", "0.4.0.18", "0.4.0.3", false)]
    [InlineData("1", "0.5.0.0", "0.4.0.3", false)]
    [InlineData("3", "0.5.0.0", "0.4.0.3", false)]
    [InlineData("2", "invalid", "0.4.0.3", false)]
    [InlineData("2", "0.11.0.0", "invalid", false)]
    [InlineData("2", "0.11.0.0", "0.11.0.1", false)]
    public void Maintenance_versions_share_one_protocol_floor_and_incompatible_services_fail_closed(string api, string server, string minimumClient, bool accepted)
    {
        var capabilities = new CapabilitiesResponse(api, server, minimumClient, false, "", [], [], [], null!);
        var error = Record.Exception(() => ServiceCompatibility.Require(capabilities, new Version(0, 8, 0, 0)));
        if (accepted) Assert.Null(error);
        else Assert.IsType<InvalidOperationException>(error);
    }
}
