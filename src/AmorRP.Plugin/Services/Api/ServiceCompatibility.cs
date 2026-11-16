using AmorRP.Contracts.Common;

namespace AmorRP.Plugin.Services.Api;

internal static class ServiceCompatibility
{
    public static void Require(CapabilitiesResponse capability, Version clientVersion)
    {
        if (capability.ApiVersion != ApiProtocol.Version
            || !Version.TryParse(capability.ServerVersion, out var serverVersion)
            || serverVersion < Version.Parse(ApiProtocol.MinimumServerVersion)
            || !Version.TryParse(capability.MinimumClientVersion, out var minimumClient)
            || minimumClient > clientVersion)
            throw new InvalidOperationException($"Amor RP requires server {ApiProtocol.MinimumServerVersion} or newer with API v{ApiProtocol.Version}. The connected server reports {capability.ServerVersion}. Update the server and plugin together.");
    }
}
