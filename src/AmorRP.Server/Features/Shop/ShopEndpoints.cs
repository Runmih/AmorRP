using AmorRP.Contracts.Shop;

namespace AmorRP.Server.Features.Shop;

public static class ShopEndpoints
{
    public static void MapShopEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/shop/products", (HttpContext h, Guid seasonId, ShopService service, CancellationToken ct) => service.ListAsync(h, seasonId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/shop/products", (HttpContext h, Guid seasonId, ShopProductWrite body, ShopService service, CancellationToken ct) => service.WriteAsync(h, seasonId, null, body, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/shop/products/{productId}", (HttpContext h, Guid seasonId, Guid productId, ShopProductWrite body, ShopService service, CancellationToken ct) => service.WriteAsync(h, seasonId, productId, body, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/shop/purchases", (HttpContext h, Guid seasonId, ShopBuy body, ShopService service, CancellationToken ct) => service.BuyAsync(h, seasonId, body, ct));
    }
}
