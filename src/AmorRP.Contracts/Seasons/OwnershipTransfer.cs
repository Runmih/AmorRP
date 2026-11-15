namespace AmorRP.Contracts.Seasons;

public sealed record OwnershipTransfer(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, Guid FromCharacterId, Guid ToCharacterId, string Status, DateTimeOffset ExpiresAt);
