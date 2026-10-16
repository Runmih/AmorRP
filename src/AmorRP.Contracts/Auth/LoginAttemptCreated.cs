namespace AmorRP.Contracts.Auth;

public sealed record LoginAttemptCreated(Guid AttemptId, string AuthorizationUrl, string AttemptCredential, DateTimeOffset ExpiresAt, int PollIntervalSeconds, bool PairingRequired = false);
