namespace AmorRP.Server.Features.Media;

public static class CurrencyIconEndpoints
{
    public static void MapCurrencyIconEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/currency/icon", (HttpContext h, Guid seasonId, CurrencyIconService s, CancellationToken ct) => s.ReadAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/currency/icon", (HttpContext h, Guid seasonId, CurrencyIconService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, true, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/currency/icon", (HttpContext h, Guid seasonId, CurrencyIconService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, false, ct));
    }
}
