namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class TradeLineRow
{
    public Guid GroupId { get; set; }
    public Guid TradeId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid HoldingId { get; set; }
    public int Quantity { get; set; }
    public string TypeId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? UseMessage { get; set; }
    public Guid? AuthorId { get; set; }
}
