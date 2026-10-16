using System.Net;
using System.Threading.RateLimiting;
using AmorRP.Server.Options;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Infrastructure.Security;

public static class ClientRateLimiting
{
    public static void AddClientRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReverseProxyOptions>()
            .Configure(options => options.UseRenderClientIp = string.Equals(configuration["RENDER"], "true", StringComparison.OrdinalIgnoreCase))
            .BindConfiguration("ReverseProxy").ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ReverseProxyOptions>>((headers, configured) =>
        {
            var proxy = configured.Value;
            headers.KnownProxies.Clear(); headers.KnownIPNetworks.Clear();
            foreach (var address in proxy.KnownProxies) headers.KnownProxies.Add(IPAddress.Parse(address));
            foreach (var network in proxy.KnownNetworks) headers.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            headers.ForwardLimit = proxy.ForwardLimit;
            // Empty trust configuration means direct-client IPs only, never trust-all.
            headers.ForwardedHeaders = proxy.KnownProxies.Length + proxy.KnownNetworks.Length > 0
                ? ForwardedHeaders.XForwardedFor : ForwardedHeaders.None;
        });
        services.AddRateLimiter(options =>
        {
            var policy = configuration.GetSection("ServicePolicy").Get<ServicePolicyOptions>() ?? new();
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/health") ? RateLimitPartition.GetNoLimiter("health")
                : Partition(context, policy.ApiRequestsPerMinute));
            options.AddPolicy("identity-probe", context => Partition(context, policy.LoginRequestsPerMinute));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await Results.Problem(statusCode: 429, title: "Request limit reached. Try again shortly.",
                    extensions: new Dictionary<string, object?> { ["code"] = "rate_limited", ["requestId"] = context.HttpContext.TraceIdentifier })
                    .ExecuteAsync(context.HttpContext);
            };
        });
    }

    private static RateLimitPartition<string> Partition(HttpContext context, int permits)
        => RateLimitPartition.GetFixedWindowLimiter(ClientNetwork.Key(context), _ => new()
        { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
}
