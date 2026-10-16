using System.Net;
using AmorRP.Server.Options;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Infrastructure.Security;

/// <summary>
/// Render's public ingress preserves Cloudflare's overwritten visitor-IP header.
/// Use it only for rate accounting, without changing connection/authentication data.
/// The public managed ingress is the trust boundary; private callers must use it too.
/// </summary>
public sealed class RenderClientIpMiddleware(RequestDelegate next, IOptions<ReverseProxyOptions> options)
{
    private static readonly object ClientIpKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!options.Value.UseRenderClientIp || context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var values = context.Request.Headers["CF-Connecting-IP"];
        var value = values.Count == 1 ? values[0] : null;
        // Exactly one plain IP literal, never a chain, host:port, scoped address,
        // or legacy numeric IPv4 notation accepted by IPAddress.TryParse.
        if (value == null || value.Any(char.IsWhiteSpace) || value.IndexOfAny(['%', '[', ']']) >= 0 ||
            !IPAddress.TryParse(value, out var address) ||
            (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && value != address.ToString()))
        {
            context.Response.Headers.CacheControl = "no-store";
            await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Client network information is unavailable. Use the public server address.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "client_network_unavailable", ["requestId"] = context.TraceIdentifier
                }).ExecuteAsync(context);
            return;
        }

        context.Items[ClientIpKey] = address;
        await next(context);
    }

    public static IPAddress? GetClientIp(HttpContext context)
        => context.Items.TryGetValue(ClientIpKey, out var address) ? (IPAddress)address! : context.Connection.RemoteIpAddress;
}
