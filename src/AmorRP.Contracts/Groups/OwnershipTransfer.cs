namespace AmorRP.Contracts.Groups;

public sealed record OwnershipTransfer(Guid Id, Guid GroupId, Guid FromCharacterId, Guid ToCharacterId, string Status, DateTimeOffset ExpiresAt);
