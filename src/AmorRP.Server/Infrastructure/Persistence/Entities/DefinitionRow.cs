namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class DefinitionRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroupId { get; set; }
    public string TypeId { get; set; } = "potion";
    public int Revision { get; set; } = 1;
    public bool Retired { get; set; }
}
