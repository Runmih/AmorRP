namespace AmorRP.Contracts.Inventory;

public sealed record Quotas(Guid CommunityId, Guid CharacterId, [property: System.Text.Json.Serialization.JsonPropertyName("quotas")] Quota[] Items);
