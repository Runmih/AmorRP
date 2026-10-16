namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class LoginRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string CredentialHash { get; set; } = "";
    public string StateHash { get; set; } = "";
    public string ProtectedVerifier { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string HomeWorldId { get; set; } = "";
    public string HomeWorldName { get; set; } = "";
    public Guid? ExistingCharacterId { get; set; }
    public string Status { get; set; } = "pending";
    public string? FailureCode { get; set; }
    public Guid? CharacterId { get; set; }
    public string? ProtectedProviderRefresh { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public string? OriginHash { get; set; }
    public bool StateUsed { get; set; }
    public Guid? SessionId { get; set; }
    public string? PairingCodeHash { get; set; }
    public int PairingFailures { get; set; }
}
