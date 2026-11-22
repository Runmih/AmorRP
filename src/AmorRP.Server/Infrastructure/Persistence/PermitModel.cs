using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class PermitListingRow
{
    public Guid CommunityId { get; set; }
    public Guid CharacterId { get; set; }
    public string Status { get; set; } = "pending";
    public int Version { get; set; } = 1;
}

public sealed class PermitOfferRow
{
    public Guid CommunityId { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CharacterId { get; set; }
    public Guid BuyerId { get; set; }
    public Guid GroupId { get; set; }
    public bool Private { get; set; }
    public int? ListingVersion { get; set; }
    public string Status { get; set; } = "pending";
    public string? AcceptedQuote { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class OwnershipPermitRow
{
    public Guid CommunityId { get; set; }
    public Guid Id { get; set; }
    public Guid CharacterId { get; set; }
    public Guid GroupId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

public sealed partial class AmorDbContext
{
    public DbSet<PermitListingRow> PermitListings => Set<PermitListingRow>();
    public DbSet<PermitOfferRow> PermitOffers => Set<PermitOfferRow>();
    public DbSet<OwnershipPermitRow> OwnershipPermits => Set<OwnershipPermitRow>();

    private static void ConfigurePermits(ModelBuilder model)
    {
        model.Entity<CommunityRow>().ToTable(t => t.HasCheckConstraint("ck_permit_price", """ "PermitPrice" > 0 """));
        model.Entity<PermitListingRow>(e =>
        {
            e.ToTable("permit_listings", t => t.HasCheckConstraint("ck_permit_listing_status", """ "Status" IN ('pending', 'listed', 'withdrawn', 'completed') """));
            e.HasKey(x => new { x.CommunityId, x.CharacterId });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PermitOfferRow>(e =>
        {
            e.ToTable("permit_offers", t => t.HasCheckConstraint("ck_permit_offer_status", """ "Status" IN ('pending', 'withdrawn', 'completed') AND "CharacterId" <> "BuyerId" """));
            e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.CharacterId, x.Status, x.ExpiresAt });
            e.HasIndex(x => new { x.CommunityId, x.BuyerId, x.Status, x.ExpiresAt });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, CharacterId = x.BuyerId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<OwnershipPermitRow>(e =>
        {
            e.ToTable("ownership_permits"); e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.CharacterId }).IsUnique().HasFilter(""" "ReleasedAt" IS NULL """);
            e.HasIndex(x => new { x.CommunityId, x.GroupId, x.ReleasedAt });
            e.HasOne<PermitOfferRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
