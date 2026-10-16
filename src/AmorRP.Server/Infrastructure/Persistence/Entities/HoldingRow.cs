namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class HoldingRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroupId { get; set; }
    public Guid CharacterId { get; set; }
    public string TypeId { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid CategoryId { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }
    public int Version { get; set; } = 1;
    public Guid? DefinitionId { get; set; }
    public int? DefinitionRevision { get; set; }
    public Guid? LetterId { get; set; }
}
