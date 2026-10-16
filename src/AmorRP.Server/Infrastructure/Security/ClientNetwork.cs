using System.Net;

namespace AmorRP.Server.Infrastructure.Security;

public static class ClientNetwork
{
    public static string Key(HttpContext context)
    {
        var address = RenderClientIpMiddleware.GetClientIp(context);
        if (address == null) return "unknown-network";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return address.ToString();
        // IPv6 privacy-address rotation within one /64 must not mint allowances.
        var bytes = address.GetAddressBytes(); Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
