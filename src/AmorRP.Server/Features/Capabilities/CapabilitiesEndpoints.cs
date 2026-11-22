using AmorRP.Contracts.Common;
using AmorRP.Core.Items;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Capabilities;

public static class CapabilitiesEndpoints
{
    public static void MapCapabilitiesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/capabilities", (IOptions<ServicePolicyOptions> policy) =>
            new CapabilitiesResponse(ApiProtocol.Version, typeof(Program).Assembly.GetName().Version!.ToString(), ApiProtocol.MinimumClientVersion, false,
                "Seasons, RP groups, shops, permits, gathering, crafting, writing, trading and events.",
                CraftingTypes.All.Select(x => new ItemTypeInfo(x, 1, CraftingTypes.IsConsumable(x), CraftingTypes.IsConsumable(x))).ToArray(), AmorRP.Server.Features.Seasons.SeasonAccess.GrantableActions, ["emote", "say", "party", "linkshell", "crossworld-linkshell"], policy.Value.ToContract()));
    }
}
