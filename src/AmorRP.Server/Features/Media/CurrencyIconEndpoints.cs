namespace AmorRP.Server.Features.Media;

public static class CurrencyIconEndpoints
{
    public static void MapCurrencyIconEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/groups/{groupId}/currency/icon", (HttpContext h, Guid groupId, CurrencyIconService s, CancellationToken ct) => s.ReadAsync(h, groupId, ct));
        app.MapPut("/api/v1/groups/{groupId}/currency/icon", (HttpContext h, Guid groupId, CurrencyIconService s, CancellationToken ct) => s.ChangeAsync(h, groupId, true, ct));
        app.MapDelete("/api/v1/groups/{groupId}/currency/icon", (HttpContext h, Guid groupId, CurrencyIconService s, CancellationToken ct) => s.ChangeAsync(h, groupId, false, ct));
    }
}
