namespace AmorRP.Contracts.Auth;

public sealed record SessionTokens(Guid SessionId, Character Character, string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt);
