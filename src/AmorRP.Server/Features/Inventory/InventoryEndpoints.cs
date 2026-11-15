using AmorRP.Contracts.Inventory;
namespace AmorRP.Server.Features.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPatch("/api/v2/seasons/{seasonId}/members/{characterId}/holdings/{holdingId}", (HttpContext h, Guid seasonId, Guid characterId, Guid holdingId, HoldingChange b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, holdingId, b, ct, characterId));
        app.MapDelete("/api/v2/seasons/{seasonId}/members/{characterId}/holdings/{holdingId}", (HttpContext h, Guid seasonId, Guid characterId, Guid holdingId, [Microsoft.AspNetCore.Mvc.FromBody] HoldingDeletion b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, holdingId, b, ct, characterId));
        app.MapGet("/api/v2/seasons/{seasonId}/quotas", (HttpContext h, Guid seasonId, InventoryService s, CancellationToken ct) => s.QuotasAsync(h, seasonId, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/inventory", (HttpContext h, Guid seasonId, InventoryService s, CancellationToken ct) => s.ListAsync(h, seasonId, null, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/members/{characterId}/holdings", (HttpContext h, Guid seasonId, Guid characterId, InventoryService s, CancellationToken ct) => s.ListAsync(h, seasonId, characterId, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/inventory/{holdingId}", (HttpContext h, Guid seasonId, Guid holdingId, InventoryService s, CancellationToken ct) => s.DetailsAsync(h, seasonId, holdingId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/inventory/potions", (HttpContext h, Guid seasonId, PotionCreate b, InventoryService s, CancellationToken ct) => s.ProduceAsync(h, seasonId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/inventory/letters", (HttpContext h, Guid seasonId, LetterCreate b, InventoryService s, CancellationToken ct) => s.ProduceAsync(h, seasonId, b, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/letters/{letterId}", (HttpContext h, Guid seasonId, Guid letterId, InventoryService s, CancellationToken ct) => s.LetterAsync(h, seasonId, letterId, null, ct));
        app.MapPatch("/api/v2/seasons/{seasonId}/letters/{letterId}", (HttpContext h, Guid seasonId, Guid letterId, LetterEdit b, InventoryService s, CancellationToken ct) => s.LetterAsync(h, seasonId, letterId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/inventory/{holdingId}/use", (HttpContext h, Guid seasonId, Guid holdingId, ConsumptionRequest b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, holdingId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/inventory/{holdingId}/discard", (HttpContext h, Guid seasonId, Guid holdingId, DiscardRequest b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, holdingId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/inventory/removals", (HttpContext h, Guid seasonId, InventoryRemoval b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, seasonId, null, b, ct));
    }
}
