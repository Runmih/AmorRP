using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class ServiceCompatibilityTests
{
    [Theory]
    [InlineData("2", "0.4.0.3", "0.4.0.3", true)]
    [InlineData("2", "0.4.0.4", "0.4.0.3", true)]
    [InlineData("2", "0.4.0.4", "0.4.0.4", true)]
    [InlineData("2", "0.4.0.2", "0.4.0.2", false)]
    [InlineData("1", "0.4.0.4", "0.4.0.3", false)]
    [InlineData("3", "0.4.0.4", "0.4.0.3", false)]
    [InlineData("2", "invalid", "0.4.0.3", false)]
    [InlineData("2", "0.4.0.4", "invalid", false)]
    [InlineData("2", "0.4.0.4", "0.5.0.0", false)]
    public void Maintenance_versions_share_one_protocol_floor_and_incompatible_services_fail_closed(string api, string server, string minimumClient, bool accepted)
    {
        var capabilities = new CapabilitiesResponse(api, server, minimumClient, false, "", [], [], [], null!);
        var error = Record.Exception(() => ServiceCompatibility.Require(capabilities, new Version(0, 4, 0, 4)));
        if (accepted) Assert.Null(error);
        else Assert.IsType<InvalidOperationException>(error);
    }
}
