namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class CategoryRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = "";
    public string[] AllowedTypeIds { get; set; } = [];
    public bool Retired { get; set; }
    public int Version { get; set; } = 1;
}
