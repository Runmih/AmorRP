namespace AmorRP.Server.Infrastructure.Persistence;

// A group belongs to one community; its approved treasury uses that community's currency.
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
    public string[] EnabledFeatures { get; set; } = AmorRP.Contracts.Groups.GroupCapabilities.DefaultFeatures.ToArray();
    public long TreasuryOwned { get; set; }
    public int TreasuryVersion { get; set; } = 1;
}
