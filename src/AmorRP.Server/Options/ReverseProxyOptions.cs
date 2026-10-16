using System.ComponentModel.DataAnnotations;
using System.Net;

namespace AmorRP.Server.Options;

public sealed class ReverseProxyOptions : IValidatableObject
{
    // Defaults to true only when the hosting platform supplies RENDER=true.
    public bool UseRenderClientIp { get; set; }
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];
    [Range(1, 5)] public int ForwardLimit { get; set; } = 1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UseRenderClientIp && KnownProxies.Length + KnownNetworks.Length > 0)
            yield return new("Render client IP mode cannot be combined with X-Forwarded-For proxy configuration.", [nameof(UseRenderClientIp)]);
        if (KnownProxies.Any(value => !IPAddress.TryParse(value, out var address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)))
            yield return new("KnownProxies must contain exact proxy IP addresses.", [nameof(KnownProxies)]);
        if (KnownNetworks.Any(value => !IPNetwork.TryParse(value, out var network) || network.PrefixLength == 0))
            yield return new("KnownNetworks must contain explicit proxy CIDRs; trusting every address is not supported.", [nameof(KnownNetworks)]);
    }
}
