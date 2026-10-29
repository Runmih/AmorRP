namespace AmorRP.Server.Infrastructure.Persistence;

// One immutable movement records both sides of a conserved currency transfer.
public sealed class TreasuryEntryRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ActorCharacterId { get; set; }
    public Guid CharacterId { get; set; }
    public string CharacterName { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Amount { get; set; }
    public long GroupBefore { get; set; }
    public long GroupAfter { get; set; }
    public long CharacterBefore { get; set; }
    public long CharacterAfter { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
