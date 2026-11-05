using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext
{
    public DbSet<GroupTrustRow> GroupTrusts => Set<GroupTrustRow>();
    public DbSet<BillRow> Bills => Set<BillRow>();
    public DbSet<BillPaymentRow> BillPayments => Set<BillPaymentRow>();
    private static void ConfigureBilling(ModelBuilder m)
    {
        m.Entity<GroupTrustRow>(e => {
            e.ToTable("group_trusts"); e.HasKey(x => new { x.CommunityId, x.GroupId, x.CharacterId });
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<BillRow>(e => {
            e.ToTable("bills", t => t.HasCheckConstraint("ck_bill_amount", "\"Amount\" > 0 AND \"Collected\" >= 0 AND \"Collected\" <= \"Amount\""));
            e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.CharacterId, x.CreatedAt, x.Id }).HasFilter("\"Collected\" < \"Amount\"");
            e.HasIndex(x => new { x.CommunityId, x.GroupId, x.IssuerCharacterId, x.CharacterId, x.CreatedAt });
            e.Property(x => x.Service).HasColumnType("text"); e.Property(x => x.RequestHash).HasMaxLength(64);
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.IssuerCharacterId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<BillPaymentRow>(e => {
            e.ToTable("bill_payments", t => t.HasCheckConstraint("ck_bill_payment", "\"Amount\" > 0 AND \"CharacterAfter\" >= 0 AND \"TreasuryBefore\" >= 0 AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\" AND \"TreasuryAfter\"::numeric - \"TreasuryBefore\"::numeric = \"Amount\""));
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.CommunityId, x.BillId, x.Id });
            e.HasOne<BillRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.BillId }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Source).HasMaxLength(40);
        });
        m.Entity<MembershipRow>().HasIndex(x => new { x.CollectionPending, x.LastCollectionAttempt });
    }
}
