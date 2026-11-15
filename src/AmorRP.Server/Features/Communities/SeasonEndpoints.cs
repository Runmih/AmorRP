using AmorRP.Contracts.Communities;

namespace AmorRP.Server.Features.Communities;

public static class SeasonEndpoints
{
    public static void MapSeasonEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/context", (HttpContext h, SeasonService s, CancellationToken ct) => s.ContextAsync(h, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/activation", (HttpContext h, Guid seasonId, SeasonTransition b, SeasonService s, CancellationToken ct) => s.TransitionAsync(h, seasonId, b, true, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/archive", (HttpContext h, Guid seasonId, SeasonTransition b, SeasonService s, CancellationToken ct) => s.TransitionAsync(h, seasonId, b, false, ct));
    }
}
