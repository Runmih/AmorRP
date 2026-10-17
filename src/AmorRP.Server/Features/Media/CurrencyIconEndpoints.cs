namespace AmorRP.Server.Features.Media;

public static class CurrencyIconEndpoints
{
    public static void MapCurrencyIconEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/currency/icon", (HttpContext h, Guid communityId, CurrencyIconService s, CancellationToken ct) => s.ReadAsync(h, communityId, ct));
        app.MapPut("/api/v2/communities/{communityId}/currency/icon", (HttpContext h, Guid communityId, CurrencyIconService s, CancellationToken ct) => s.ChangeAsync(h, communityId, true, ct));
        app.MapDelete("/api/v2/communities/{communityId}/currency/icon", (HttpContext h, Guid communityId, CurrencyIconService s, CancellationToken ct) => s.ChangeAsync(h, communityId, false, ct));
    }
}
