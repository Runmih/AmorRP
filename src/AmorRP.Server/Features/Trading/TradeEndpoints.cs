using AmorRP.Contracts.Trading;
namespace AmorRP.Server.Features.Trading;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/trades", (HttpContext h, Guid seasonId, TradeService s, CancellationToken ct) => s.ReadAsync(h, seasonId, null, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/trades", (HttpContext h, Guid seasonId, TradeCreate b, TradeService s, CancellationToken ct) => s.CommandAsync(h, seasonId, null, b, "create", ct));
        app.MapGet("/api/v2/seasons/{seasonId}/trades/{tradeId}", (HttpContext h, Guid seasonId, Guid tradeId, TradeService s, CancellationToken ct) => s.ReadAsync(h, seasonId, tradeId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/trades/{tradeId}/accept", (HttpContext h, Guid seasonId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, seasonId, tradeId, b, "accept", ct));
        app.MapPut("/api/v2/seasons/{seasonId}/trades/{tradeId}/offers/mine", (HttpContext h, Guid seasonId, Guid tradeId, TradeOfferWrite b, TradeService s, CancellationToken ct) => s.CommandAsync(h, seasonId, tradeId, b, "offer", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/trades/{tradeId}/confirm", (HttpContext h, Guid seasonId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, seasonId, tradeId, b, "confirm", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/trades/{tradeId}/final-confirm", (HttpContext h, Guid seasonId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, seasonId, tradeId, b, "final-confirm", ct));
        app.MapPost("/api/v2/seasons/{seasonId}/trades/{tradeId}/cancel", (HttpContext h, Guid seasonId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, seasonId, tradeId, b, "cancel", ct));
    }
}
