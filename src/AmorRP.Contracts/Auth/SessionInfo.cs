namespace AmorRP.Contracts.Auth;

public sealed record SessionInfo(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt, DateTimeOffset ExpiresAt, bool IsCurrent);
