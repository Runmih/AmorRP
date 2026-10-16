namespace AmorRP.Contracts.History;

public sealed record Operation(Guid Id, Guid? GroupId, string Kind, Guid? ActorCharacterId, DateTimeOffset OccurredAt, Guid[] ResourceIds);
