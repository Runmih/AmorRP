namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class SeasonCreationPermissionRow
{
    public Guid CharacterId { get; set; }
    public bool CanCreate { get; set; }
    public int Version { get; set; } = 1;
}
