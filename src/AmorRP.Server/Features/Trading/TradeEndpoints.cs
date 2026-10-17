using AmorRP.Contracts.Trading;
namespace AmorRP.Server.Features.Trading;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/trades", (HttpContext h, Guid communityId, TradeService s, CancellationToken ct) => s.ReadAsync(h, communityId, null, ct));
        app.MapPost("/api/v2/communities/{communityId}/trades", (HttpContext h, Guid communityId, TradeCreate b, TradeService s, CancellationToken ct) => s.CommandAsync(h, communityId, null, b, "create", ct));
        app.MapGet("/api/v2/communities/{communityId}/trades/{tradeId}", (HttpContext h, Guid communityId, Guid tradeId, TradeService s, CancellationToken ct) => s.ReadAsync(h, communityId, tradeId, ct));
        app.MapPost("/api/v2/communities/{communityId}/trades/{tradeId}/accept", (HttpContext h, Guid communityId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, communityId, tradeId, b, "accept", ct));
        app.MapPut("/api/v2/communities/{communityId}/trades/{tradeId}/offers/mine", (HttpContext h, Guid communityId, Guid tradeId, TradeOfferWrite b, TradeService s, CancellationToken ct) => s.CommandAsync(h, communityId, tradeId, b, "offer", ct));
        app.MapPost("/api/v2/communities/{communityId}/trades/{tradeId}/confirm", (HttpContext h, Guid communityId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, communityId, tradeId, b, "confirm", ct));
        app.MapPost("/api/v2/communities/{communityId}/trades/{tradeId}/final-confirm", (HttpContext h, Guid communityId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, communityId, tradeId, b, "final-confirm", ct));
        app.MapPost("/api/v2/communities/{communityId}/trades/{tradeId}/cancel", (HttpContext h, Guid communityId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, communityId, tradeId, b, "cancel", ct));
    }
}
