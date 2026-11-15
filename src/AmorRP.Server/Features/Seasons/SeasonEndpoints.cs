using AmorRP.Contracts.Seasons;

namespace AmorRP.Server.Features.Seasons;

public static class SeasonEndpoints
{
    public static void MapSeasonEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/context", (HttpContext h, SeasonService s, CancellationToken ct) => s.ContextAsync(h, ct));
        app.MapGet("/api/v2/seasons/creation-permissions", (HttpContext h, SeasonService s, CancellationToken ct) => s.CreationPermissionsAsync(h, ct));
        app.MapPut("/api/v2/seasons/creation-permissions/{characterId}", (HttpContext h, Guid characterId, SeasonCreationPermissionEdit b, SeasonService s, CancellationToken ct) => s.CreationPermissionAsync(h, characterId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/activation", (HttpContext h, Guid seasonId, SeasonTransition b, SeasonService s, CancellationToken ct) => s.TransitionAsync(h, seasonId, b, true, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/archive", (HttpContext h, Guid seasonId, SeasonTransition b, SeasonService s, CancellationToken ct) => s.TransitionAsync(h, seasonId, b, false, ct));
    }
}
