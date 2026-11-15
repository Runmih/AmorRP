namespace AmorRP.Contracts.Inventory;

public sealed record Letter(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid CommunityId, AmorRP.Contracts.Auth.Character Author, Guid CategoryId, string Title, string Body, int Version, bool Locked, DateTimeOffset? FirstTradedAt, string TypeId = "letter");
