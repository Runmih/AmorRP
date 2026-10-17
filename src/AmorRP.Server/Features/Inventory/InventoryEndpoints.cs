using AmorRP.Contracts.Inventory;
namespace AmorRP.Server.Features.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPatch("/api/v2/communities/{communityId}/members/{characterId}/holdings/{holdingId}", (HttpContext h, Guid communityId, Guid characterId, Guid holdingId, HoldingChange b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, communityId, holdingId, b, ct, characterId));
        app.MapDelete("/api/v2/communities/{communityId}/members/{characterId}/holdings/{holdingId}", (HttpContext h, Guid communityId, Guid characterId, Guid holdingId, [Microsoft.AspNetCore.Mvc.FromBody] HoldingDeletion b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, communityId, holdingId, b, ct, characterId));
        app.MapGet("/api/v2/communities/{communityId}/quotas", (HttpContext h, Guid communityId, InventoryService s, CancellationToken ct) => s.QuotasAsync(h, communityId, ct));
        app.MapGet("/api/v2/communities/{communityId}/inventory", (HttpContext h, Guid communityId, InventoryService s, CancellationToken ct) => s.ListAsync(h, communityId, null, ct));
        app.MapGet("/api/v2/communities/{communityId}/members/{characterId}/holdings", (HttpContext h, Guid communityId, Guid characterId, InventoryService s, CancellationToken ct) => s.ListAsync(h, communityId, characterId, ct));
        app.MapGet("/api/v2/communities/{communityId}/inventory/{holdingId}", (HttpContext h, Guid communityId, Guid holdingId, InventoryService s, CancellationToken ct) => s.DetailsAsync(h, communityId, holdingId, ct));
        app.MapPost("/api/v2/communities/{communityId}/inventory/potions", (HttpContext h, Guid communityId, PotionCreate b, InventoryService s, CancellationToken ct) => s.ProduceAsync(h, communityId, b, ct));
        app.MapPost("/api/v2/communities/{communityId}/inventory/letters", (HttpContext h, Guid communityId, LetterCreate b, InventoryService s, CancellationToken ct) => s.ProduceAsync(h, communityId, b, ct));
        app.MapGet("/api/v2/communities/{communityId}/letters/{letterId}", (HttpContext h, Guid communityId, Guid letterId, InventoryService s, CancellationToken ct) => s.LetterAsync(h, communityId, letterId, null, ct));
        app.MapPatch("/api/v2/communities/{communityId}/letters/{letterId}", (HttpContext h, Guid communityId, Guid letterId, LetterEdit b, InventoryService s, CancellationToken ct) => s.LetterAsync(h, communityId, letterId, b, ct));
        app.MapPost("/api/v2/communities/{communityId}/inventory/{holdingId}/use", (HttpContext h, Guid communityId, Guid holdingId, ConsumptionRequest b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, communityId, holdingId, b, ct));
        app.MapPost("/api/v2/communities/{communityId}/inventory/{holdingId}/discard", (HttpContext h, Guid communityId, Guid holdingId, DiscardRequest b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, communityId, holdingId, b, ct));
        app.MapPost("/api/v2/communities/{communityId}/inventory/removals", (HttpContext h, Guid communityId, InventoryRemoval b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, communityId, null, b, ct));
    }
}
