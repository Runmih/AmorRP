using AmorRP.Contracts.Inventory;

namespace AmorRP.Server.Features.Moodles;

public static class MoodleEndpoints
{
    public static void MapMoodleEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/me/moodle-effects", (HttpContext h, MoodleService s, CancellationToken ct) => s.ReadAsync(h, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/ownership-effect", (HttpContext h, Guid seasonId, MoodleService s, CancellationToken ct) => s.PolicyAsync(h, seasonId, null, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/ownership-effect", (HttpContext h, Guid seasonId, OwnershipEffectPolicy b, MoodleService s, CancellationToken ct) => s.PolicyAsync(h, seasonId, b, ct));
    }
}
