using AmorRP.Contracts.Trading;
namespace AmorRP.Server.Features.Trading;

public static class TradeEndpoints
{
    public static void MapTradeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/groups/{groupId}/trades", (HttpContext h, Guid groupId, TradeService s, CancellationToken ct) => s.ReadAsync(h, groupId, null, ct));
        app.MapPost("/api/v1/groups/{groupId}/trades", (HttpContext h, Guid groupId, TradeCreate b, TradeService s, CancellationToken ct) => s.CommandAsync(h, groupId, null, b, "create", ct));
        app.MapGet("/api/v1/groups/{groupId}/trades/{tradeId}", (HttpContext h, Guid groupId, Guid tradeId, TradeService s, CancellationToken ct) => s.ReadAsync(h, groupId, tradeId, ct));
        app.MapPost("/api/v1/groups/{groupId}/trades/{tradeId}/accept", (HttpContext h, Guid groupId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, groupId, tradeId, b, "accept", ct));
        app.MapPut("/api/v1/groups/{groupId}/trades/{tradeId}/offers/mine", (HttpContext h, Guid groupId, Guid tradeId, TradeOfferWrite b, TradeService s, CancellationToken ct) => s.CommandAsync(h, groupId, tradeId, b, "offer", ct));
        app.MapPost("/api/v1/groups/{groupId}/trades/{tradeId}/confirm", (HttpContext h, Guid groupId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, groupId, tradeId, b, "confirm", ct));
        app.MapPost("/api/v1/groups/{groupId}/trades/{tradeId}/final-confirm", (HttpContext h, Guid groupId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, groupId, tradeId, b, "final-confirm", ct));
        app.MapPost("/api/v1/groups/{groupId}/trades/{tradeId}/cancel", (HttpContext h, Guid groupId, Guid tradeId, TradeRevision b, TradeService s, CancellationToken ct) => s.CommandAsync(h, groupId, tradeId, b, "cancel", ct));
    }
}
