namespace AmorRP.Contracts.Auth;

public sealed record LoginAttemptStatus(Guid AttemptId, string Status, DateTimeOffset ExpiresAt, string? FailureCode);
