namespace AmorRP.Server.Features.Media;

public static class SeasonLogoEndpoints
{
    public static void MapSeasonLogoEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/logo", (HttpContext h, Guid seasonId, SeasonLogoService s, CancellationToken ct) => s.ReadAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/logo", (HttpContext h, Guid seasonId, SeasonLogoService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, true, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/logo", (HttpContext h, Guid seasonId, SeasonLogoService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, false, ct));
    }
}
