using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext
{
    public DbSet<TreasuryEntryRow> TreasuryEntries => Set<TreasuryEntryRow>();
    private static void ConfigureTreasury(ModelBuilder m)
    {
        m.Entity<TreasuryEntryRow>(e => {
            e.ToTable("treasury_entries", t => {
                t.HasCheckConstraint("ck_treasury_entry_amount", "\"Amount\" > 0 AND \"GroupBefore\" >= 0 AND \"GroupAfter\" >= 0 AND \"CharacterBefore\" >= 0 AND \"CharacterAfter\" >= 0");
                t.HasCheckConstraint("ck_treasury_entry_conservation", "(\"Kind\" = 'deposit' AND \"GroupAfter\"::numeric - \"GroupBefore\"::numeric = \"Amount\" AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\") OR (\"Kind\" IN ('withdrawal', 'disband_return') AND \"GroupBefore\"::numeric - \"GroupAfter\"::numeric = \"Amount\" AND \"CharacterAfter\"::numeric - \"CharacterBefore\"::numeric = \"Amount\")");
            });
            e.HasKey(x => x.Id); e.HasIndex(x => new { x.CommunityId, x.GroupId, x.Id });
            e.HasIndex(x => new { x.OperationId, x.GroupId }).IsUnique();
            e.Property(x => x.Note).HasColumnType("text"); e.Property(x => x.Kind).HasMaxLength(30); e.Property(x => x.CharacterName).HasMaxLength(80);
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OperationRow>().WithMany().HasForeignKey(x => x.OperationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.ActorCharacterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
