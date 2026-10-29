namespace AmorRP.Server.Infrastructure.Persistence;

// A group is a profile within a community. Currency, inventory and permissions
// belong to its community; this record does not silently create another economy.
public sealed class GroupRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? LogoAssetId { get; set; }
    public int LogoVersion { get; set; } = 1;
    public int Version { get; set; } = 1;
    public Guid OwnerCharacterId { get; set; }
    public string JoinMode { get; set; } = "open";
    public string? PasswordHash { get; set; }
    public bool Deleted { get; set; }
    public string[] EnabledFeatures { get; set; } = AmorRP.Contracts.Groups.GroupCapabilities.All.ToArray();
}
