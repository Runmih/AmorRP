using AmorRP.Contracts.Common;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Capabilities;

public static class CapabilitiesEndpoints
{
    public static void MapCapabilitiesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/capabilities", (IOptions<ServicePolicyOptions> policy) =>
            new CapabilitiesResponse("2", "0.0.6.17", "0.0.6.6", false,
                "M5 private usability candidate: communities, currency, literal potion messages, Books writing and persistent trading.",
                [new("potion", 1, true, true), new("letter", 1, false, false), new("book", 1, false, false), new("note", 1, false, false)], AmorRP.Server.Features.Communities.CommunityAccess.GrantableActions, ["emote", "say", "party", "linkshell", "crossworld-linkshell"], policy.Value.ToContract()));
    }
}
