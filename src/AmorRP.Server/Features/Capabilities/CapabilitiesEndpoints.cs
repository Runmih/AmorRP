using AmorRP.Contracts.Common;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Capabilities;

public static class CapabilitiesEndpoints
{
    public static void MapCapabilitiesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/capabilities", (IOptions<ServicePolicyOptions> policy) =>
            new CapabilitiesResponse("1", "0.0.6.14", "0.0.6.6", false,
                "M5 private usability candidate: groups, currency, literal potion messages, Books writing and persistent trading.",
                [new("potion", 1, true, true), new("letter", 1, false, false), new("book", 1, false, false), new("note", 1, false, false)], AmorRP.Server.Features.Groups.GroupAccess.GrantableActions, ["emote", "say", "party", "linkshell", "crossworld-linkshell"], policy.Value.ToContract()));
    }
}
