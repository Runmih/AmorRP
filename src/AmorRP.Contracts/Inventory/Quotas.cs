namespace AmorRP.Contracts.Inventory;

public sealed record Quotas([property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, Guid CharacterId, [property: System.Text.Json.Serialization.JsonPropertyName("quotas")] Quota[] Items);
