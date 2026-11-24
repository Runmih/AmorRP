using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class PermitTransferRow
{
    public Guid CommunityId { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PermitId { get; set; }
    public Guid SellerGroupId { get; set; }
    public Guid BuyerGroupId { get; set; }
    public Guid SellerId { get; set; }
    public Guid BuyerId { get; set; }
    public long Price { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string State { get; set; } = "pending";
}

public sealed partial class AmorDbContext
{
    public DbSet<PermitTransferRow> PermitTransfers => Set<PermitTransferRow>();

    private static void ConfigurePermitTransfers(ModelBuilder model)
    {
        model.Entity<PermitTransferRow>(e =>
        {
            e.ToTable("permit_transfers", t => t.HasCheckConstraint("ck_permit_transfer",
                """ "Price" >= 0 AND "SellerGroupId" <> "BuyerGroupId" AND "State" IN ('pending', 'approved', 'cancelled', 'completed') """));
            e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.PermitId, x.State, x.ExpiresAt });
            e.HasOne<OwnershipPermitRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.PermitId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.SellerGroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.BuyerGroupId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
