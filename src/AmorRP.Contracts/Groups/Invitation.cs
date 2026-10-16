namespace AmorRP.Contracts.Groups;

public sealed record Invitation(Guid Id, Guid GroupId, DateTimeOffset ExpiresAt, int MaxUses, int Used, bool Revoked);
