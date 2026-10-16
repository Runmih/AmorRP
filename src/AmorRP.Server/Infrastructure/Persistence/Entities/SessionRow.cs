namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class SessionRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CharacterId { get; set; }
    public string AccessHash { get; set; } = "";
    public DateTimeOffset AccessExpiresAt { get; set; }
    public string RefreshHash { get; set; } = "";
    public DateTimeOffset RefreshExpiresAt { get; set; }
    public string ProtectedProviderRefresh { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public bool Revoked { get; set; }
}
