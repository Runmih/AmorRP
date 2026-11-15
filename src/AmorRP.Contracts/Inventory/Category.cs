namespace AmorRP.Contracts.Inventory;

public sealed record Category(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, string Name, string[] AllowedTypeIds, bool Retired, int Version);
