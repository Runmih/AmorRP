using AmorRP.Contracts.Common;

namespace AmorRP.Server.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => new HealthResponse("ok"));
        app.MapGet("/health/ready", (HttpContext http, ReadinessMonitor readiness) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var ready = readiness.IsReady;
            return Results.Json(new HealthResponse(ready ? "ok" : "unavailable"),
                statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
    }
}
