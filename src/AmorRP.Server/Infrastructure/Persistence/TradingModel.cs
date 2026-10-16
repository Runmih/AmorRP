using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext
{
    public DbSet<TradeRow> Trades => Set<TradeRow>();
    public DbSet<TradeSlotRow> TradeSlots => Set<TradeSlotRow>();
    public DbSet<TradeLineRow> TradeLines => Set<TradeLineRow>();
    private static void ConfigureTrading(ModelBuilder m)
    {
        m.Entity<TradeRow>(e => {
            e.ToTable("trades", t => {
                t.HasCheckConstraint("ck_trade_state", "\"State\" IN ('invited','open','confirming','completed','cancelled','expired')");
                t.HasCheckConstraint("ck_trade_participants", "\"InitiatorId\" <> \"RecipientId\"");
                t.HasCheckConstraint("ck_trade_amounts", "\"InitiatorCurrency\" >= 0 AND \"RecipientCurrency\" >= 0 AND \"Revision\" > 0");
            });
            e.HasKey(x => new { x.GroupId, x.Id }); e.HasIndex(x => new { x.State, x.ExpiresAt });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.GroupId, x.InitiatorId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.GroupId, x.RecipientId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<TradeSlotRow>(e => {
            e.ToTable("trade_slots"); e.HasKey(x => new { x.GroupId, x.CharacterId });
            e.HasOne<TradeRow>().WithMany().HasForeignKey(x => new { x.GroupId, x.TradeId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.GroupId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<TradeLineRow>(e => {
            e.ToTable("trade_lines", t => t.HasCheckConstraint("ck_trade_line_quantity", "\"Quantity\" > 0"));
            e.HasKey(x => new { x.GroupId, x.TradeId, x.CharacterId, x.HoldingId });
            e.HasOne<TradeRow>().WithMany().HasForeignKey(x => new { x.GroupId, x.TradeId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<HoldingRow>().WithMany().HasForeignKey(x => new { x.GroupId, x.HoldingId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
