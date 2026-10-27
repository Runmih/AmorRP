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
            new CapabilitiesResponse("2", "0.0.6.21", "0.0.6.18", false,
                "Communities, owned groups, Alchemy, Cooking, Writing, trading, notices and events.",
                CraftingTypes.All.Select(x => new ItemTypeInfo(x, 1, CraftingTypes.IsConsumable(x), CraftingTypes.IsConsumable(x))).ToArray(), AmorRP.Server.Features.Communities.CommunityAccess.GrantableActions, ["emote", "say", "party", "linkshell", "crossworld-linkshell"], policy.Value.ToContract()));
    }
}
