namespace AmorRP.Contracts.Currency;

public sealed record Currency(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, string Name, string Symbol, int Version, Guid? IconAssetId = null);
