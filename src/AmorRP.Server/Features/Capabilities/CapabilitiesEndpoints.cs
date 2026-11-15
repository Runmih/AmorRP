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
            new CapabilitiesResponse("2", "0.4.0.3", "0.4.0.3", false,
                "Seasons, owned RP groups, crafting, writing, trading, notices and events.",
                CraftingTypes.All.Select(x => new ItemTypeInfo(x, 1, CraftingTypes.IsConsumable(x), CraftingTypes.IsConsumable(x))).ToArray(), AmorRP.Server.Features.Seasons.SeasonAccess.GrantableActions, ["emote", "say", "party", "linkshell", "crossworld-linkshell"], policy.Value.ToContract()));
    }
}
