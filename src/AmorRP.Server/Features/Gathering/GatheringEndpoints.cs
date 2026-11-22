using AmorRP.Contracts.Inventory;

namespace AmorRP.Server.Features.Gathering;

public static class GatheringEndpoints
{
    public static void MapGatheringEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/gathering", (HttpContext h, Guid seasonId, GatheringService s, CancellationToken ct) => s.ReadAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/gathering", (HttpContext h, Guid seasonId, GatheringSettingsEdit b, GatheringService s, CancellationToken ct) => s.WriteAsync(h, seasonId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/gathering/packages", (HttpContext h, Guid seasonId, GatherSupplies b, GatheringService s, CancellationToken ct) => s.GatherAsync(h, seasonId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/inventory/{holdingId}/use-supplies", (HttpContext h, Guid seasonId, Guid holdingId, UseSupplies b, GatheringService s, CancellationToken ct) => s.UseAsync(h, seasonId, holdingId, b, ct));
    }
}
