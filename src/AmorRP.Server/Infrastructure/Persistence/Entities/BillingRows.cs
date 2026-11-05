namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class GroupTrustRow
{
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CharacterId { get; set; }
    public bool Trusted { get; set; }
    public int Version { get; set; }
}
public sealed class BillRow
{
    public Guid CommunityId { get; set; }
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = "";
    public Guid IssuerCharacterId { get; set; }
    public string IssuerName { get; set; } = "";
    public Guid CharacterId { get; set; }
    public string CharacterName { get; set; } = "";
    public string Service { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public long Amount { get; set; }
    public long Collected { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class BillPaymentRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public Guid BillId { get; set; }
    public long Amount { get; set; }
    public long CharacterBefore { get; set; }
    public long CharacterAfter { get; set; }
    public long TreasuryBefore { get; set; }
    public long TreasuryAfter { get; set; }
    public string Source { get; set; } = "";
    public Guid? SourceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
