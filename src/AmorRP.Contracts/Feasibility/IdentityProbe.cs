using System.Text.Json.Serialization;

namespace AmorRP.Contracts.Feasibility;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IdentityProbeStart(string DisplayName, string HomeWorld);
public sealed record IdentityProbeCreated(Guid AttemptId, string AuthorizationUrl, string AttemptCredential,
    DateTimeOffset ExpiresAt, int PollIntervalSeconds);
public sealed record IdentityProbeStatus(Guid AttemptId, string Status, DateTimeOffset ExpiresAt, string? FailureCode);
public sealed record VerifiedCharacter(string PersistentKey, string LodestoneId, string DisplayName, string HomeWorld);
public sealed record IdentityProbeSession(string AccessToken, DateTimeOffset ExpiresAt, VerifiedCharacter Character);
