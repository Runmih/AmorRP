namespace AmorRP.Contracts.Communities;

public sealed record Invitation(Guid Id, Guid CommunityId, DateTimeOffset ExpiresAt, int MaxUses, int Used, bool Revoked);
