namespace AmorRP.Server.Infrastructure.Persistence;

// A single unique slot covers either participant position, across all server replicas.
public sealed class TradeSlotRow
{
    public Guid CommunityId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid TradeId { get; set; }
}
