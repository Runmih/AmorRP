using AmorRP.Contracts.Inventory;
namespace AmorRP.Server.Features.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPatch("/api/v1/groups/{groupId}/members/{characterId}/holdings/{holdingId}", (HttpContext h, Guid groupId, Guid characterId, Guid holdingId, HoldingChange b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, groupId, holdingId, b, ct, characterId));
        app.MapDelete("/api/v1/groups/{groupId}/members/{characterId}/holdings/{holdingId}", (HttpContext h, Guid groupId, Guid characterId, Guid holdingId, [Microsoft.AspNetCore.Mvc.FromBody] HoldingDeletion b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, groupId, holdingId, b, ct, characterId));
        app.MapGet("/api/v1/groups/{groupId}/quotas", (HttpContext h, Guid groupId, InventoryService s, CancellationToken ct) => s.QuotasAsync(h, groupId, ct));
        app.MapGet("/api/v1/groups/{groupId}/inventory", (HttpContext h, Guid groupId, InventoryService s, CancellationToken ct) => s.ListAsync(h, groupId, null, ct));
        app.MapGet("/api/v1/groups/{groupId}/members/{characterId}/holdings", (HttpContext h, Guid groupId, Guid characterId, InventoryService s, CancellationToken ct) => s.ListAsync(h, groupId, characterId, ct));
        app.MapGet("/api/v1/groups/{groupId}/inventory/{holdingId}", (HttpContext h, Guid groupId, Guid holdingId, InventoryService s, CancellationToken ct) => s.DetailsAsync(h, groupId, holdingId, ct));
        app.MapPost("/api/v1/groups/{groupId}/inventory/potions", (HttpContext h, Guid groupId, PotionCreate b, InventoryService s, CancellationToken ct) => s.ProduceAsync(h, groupId, b, ct));
        app.MapPost("/api/v1/groups/{groupId}/inventory/letters", (HttpContext h, Guid groupId, LetterCreate b, InventoryService s, CancellationToken ct) => s.ProduceAsync(h, groupId, b, ct));
        app.MapGet("/api/v1/groups/{groupId}/letters/{letterId}", (HttpContext h, Guid groupId, Guid letterId, InventoryService s, CancellationToken ct) => s.LetterAsync(h, groupId, letterId, null, ct));
        app.MapPatch("/api/v1/groups/{groupId}/letters/{letterId}", (HttpContext h, Guid groupId, Guid letterId, LetterEdit b, InventoryService s, CancellationToken ct) => s.LetterAsync(h, groupId, letterId, b, ct));
        app.MapPost("/api/v1/groups/{groupId}/inventory/{holdingId}/use", (HttpContext h, Guid groupId, Guid holdingId, ConsumptionRequest b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, groupId, holdingId, b, ct));
        app.MapPost("/api/v1/groups/{groupId}/inventory/{holdingId}/discard", (HttpContext h, Guid groupId, Guid holdingId, DiscardRequest b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, groupId, holdingId, b, ct));
        app.MapPost("/api/v1/groups/{groupId}/inventory/removals", (HttpContext h, Guid groupId, InventoryRemoval b, InventoryService s, CancellationToken ct) => s.ChangeAsync(h, groupId, null, b, ct));
    }
}
