namespace AmorRP.Contracts.Communities;

public sealed record OwnershipTransfer(Guid Id, Guid CommunityId, Guid FromCharacterId, Guid ToCharacterId, string Status, DateTimeOffset ExpiresAt);
