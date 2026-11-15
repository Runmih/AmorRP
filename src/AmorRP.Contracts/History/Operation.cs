namespace AmorRP.Contracts.History;

public sealed record Operation(Guid Id, [property: System.Text.Json.Serialization.JsonPropertyName("seasonId")] Guid? CommunityId, string Kind, Guid? ActorCharacterId, DateTimeOffset OccurredAt, Guid[] ResourceIds);
