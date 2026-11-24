using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class ShopProductRow
{
    public Guid CommunityId { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DefinitionId { get; set; }
    public int DefinitionRevision { get; set; }
    public long Price { get; set; }
    public string Buyer { get; set; } = "character";
    public int? Stock { get; set; }
    public int? BuyerLimit { get; set; }
    public bool Listed { get; set; }
    public bool Deleted { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class ShopPurchaseRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public Guid ProductId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid? GroupId { get; set; }
    public int Quantity { get; set; }
}

public sealed partial class AmorDbContext
{
    public DbSet<ShopProductRow> ShopProducts => Set<ShopProductRow>();
    public DbSet<ShopPurchaseRow> ShopPurchases => Set<ShopPurchaseRow>();

    private static void ConfigureShop(ModelBuilder model)
    {
        model.Entity<ShopProductRow>(e =>
        {
            e.ToTable("shop_products", t => t.HasCheckConstraint("ck_shop_product",
                """ "Price" > 0 AND "Buyer" IN ('character', 'group', 'either') AND "Stock" >= 0 AND "BuyerLimit" > 0 """));
            e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasOne<DefinitionRevisionRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.DefinitionId, Revision = x.DefinitionRevision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ShopPurchaseRow>(e =>
        {
            e.ToTable("shop_purchases", t => t.HasCheckConstraint("ck_shop_purchase", """ "Quantity" > 0 """));
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CommunityId, x.ProductId, x.CharacterId });
            e.HasIndex(x => new { x.CommunityId, x.ProductId, x.GroupId });
            e.HasOne<ShopProductRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.ProductId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
