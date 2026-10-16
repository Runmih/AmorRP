namespace AmorRP.Contracts.Inventory;

public sealed record Quotas(Guid GroupId, Guid CharacterId, [property: System.Text.Json.Serialization.JsonPropertyName("quotas")] Quota[] Items);
