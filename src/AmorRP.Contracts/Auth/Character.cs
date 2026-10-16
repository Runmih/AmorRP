namespace AmorRP.Contracts.Auth;

public sealed record Character(Guid Id, string DisplayName, string HomeWorldId, string HomeWorldName, DateTimeOffset VerifiedAt, string Status);
