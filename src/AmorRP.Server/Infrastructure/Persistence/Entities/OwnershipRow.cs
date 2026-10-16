namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class OwnershipRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroupId { get; set; }
    public Guid FromCharacterId { get; set; }
    public Guid ToCharacterId { get; set; }
    public string Status { get; set; } = "proposed";
    public DateTimeOffset ExpiresAt { get; set; }
}
