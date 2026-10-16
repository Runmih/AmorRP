namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class CharacterRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string LodestoneId { get; set; } = "";
    public string OwnershipKeyHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string HomeWorldId { get; set; } = "";
    public string HomeWorldName { get; set; } = "";
    public DateTimeOffset VerifiedAt { get; set; }
}
