using System.Text;
using System.Text.Json;
using AmorRP.Contracts.Common;
using Microsoft.AspNetCore.Routing;

namespace AmorRP.Server.Infrastructure.Compatibility;

public static class LegacyApi
{
    public static bool IsLegacy(HttpContext h) => h.Request.Path.StartsWithSegments("/api/v1");
    public static string BodyJson(HttpContext h, object? body) => IsLegacy(h)
        ? LegacyCommunityJson.ToLegacy(JsonSerializer.Serialize(body, Persistence.CommandStore.Json)) : JsonSerializer.Serialize(body, Persistence.CommandStore.Json);

    public static void MapLegacyApi(this WebApplication app)
    {
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToArray();
        foreach (var endpoint in endpoints)
        {
            var path = endpoint.RoutePattern.RawText;
            // New child groups only exist in v2. The old /groups route means communities.
            if (path == null || !path.StartsWith("/api/v2/", StringComparison.Ordinal) || path.Contains("/groups", StringComparison.Ordinal)
                || path.EndsWith("/treasury-returns", StringComparison.Ordinal)
                || path.EndsWith("/group-hub", StringComparison.Ordinal) || path.EndsWith("/favorite-group-events", StringComparison.Ordinal)) continue;
            var oldPath = path.Replace("/api/v2/", "/api/v1/", StringComparison.Ordinal).Replace("/communities", "/groups", StringComparison.Ordinal);
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods;
            app.MapMethods(oldPath, methods, endpoint.RequestDelegate!).WithMetadata(endpoint.Metadata.ToArray());
        }
    }
}

public sealed class LegacyApiMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!LegacyApi.IsLegacy(context)) { await next(context); return; }
        // Preserve the external URL and If-Match for idempotency fingerprints. Only
        // legacy permission identifiers in the JSON body need adapting before binding.
        var input = context.Request.Body;
        MemoryStream? adaptedInput = null;
        if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true);
            var raw = await reader.ReadToEndAsync(context.RequestAborted);
            try { raw = LegacyCommunityJson.ToCurrent(raw); } catch (JsonException) { }
            adaptedInput = new MemoryStream(Encoding.UTF8.GetBytes(raw));
            context.Request.Body = adaptedInput; context.Request.ContentLength = adaptedInput.Length;
        }
        var output = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
            var bytes = buffer.ToArray();
            if (context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true && bytes.Length > 0)
            {
                bytes = Encoding.UTF8.GetBytes(LegacyCommunityJson.ToLegacy(Encoding.UTF8.GetString(bytes)));
                context.Response.ContentLength = bytes.Length;
            }
            if (context.Response.Headers.ETag.Count > 0) context.Response.Headers.ETag = LegacyCommunityJson.ETag(context.Response.Headers.ETag.ToString(), legacy: true);
            context.Response.Body = output;
            await output.WriteAsync(bytes, context.RequestAborted);
        }
        finally { context.Response.Body = output; context.Request.Body = input; adaptedInput?.Dispose(); }
    }
}
