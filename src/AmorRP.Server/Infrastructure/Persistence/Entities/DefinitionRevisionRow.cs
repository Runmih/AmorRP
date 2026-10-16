namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class DefinitionRevisionRow
{
    public Guid GroupId { get; set; }
    public Guid DefinitionId { get; set; }
    public int Revision { get; set; }
    public int TypeDataVersion { get; set; } = 1;
    public string Name { get; set; } = "";
    public Guid CategoryId { get; set; }
    public string Description { get; set; } = "";
    public bool Enabled { get; set; }
    public int CreationCost { get; set; }
    public string UseMessage { get; set; } = "";
}
