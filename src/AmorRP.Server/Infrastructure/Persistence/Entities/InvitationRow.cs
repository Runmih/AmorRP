namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class InvitationRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroupId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int Used { get; set; }
    public bool Revoked { get; set; }
}
