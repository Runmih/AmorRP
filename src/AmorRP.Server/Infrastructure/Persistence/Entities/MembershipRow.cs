namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class MembershipRow
{
    public Guid CommunityId { get; set; }
    public Guid CharacterId { get; set; }
    public string Status { get; set; } = "active";
    public Guid[] ReadAnnouncementIds { get; set; } = [];
    public string Description { get; set; } = "";
    public string[] Capabilities { get; set; } = [];
    public bool TradingRestricted { get; set; }
    public int Version { get; set; } = 1;
    public long Owned { get; set; }
    public long Reserved { get; set; }
    public int BalanceVersion { get; set; } = 1;
}
