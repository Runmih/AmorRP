using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext
{
    public DbSet<CategoryRow> Categories => Set<CategoryRow>();
    public DbSet<DefinitionRow> Definitions => Set<DefinitionRow>();
    public DbSet<DefinitionRevisionRow> DefinitionRevisions => Set<DefinitionRevisionRow>();
    public DbSet<LetterRow> Letters => Set<LetterRow>();
    public DbSet<HoldingRow> Holdings => Set<HoldingRow>();
    public DbSet<QuotaRow> Quotas => Set<QuotaRow>();
    public DbSet<MediaAssetRow> MediaAssets => Set<MediaAssetRow>();
    private static void ConfigureInventory(ModelBuilder m)
    {
        m.Entity<CategoryRow>(e => {
            e.ToTable("categories"); e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<DefinitionRow>(e => {
            e.ToTable("item_definitions"); e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<DefinitionRevisionRow>(e => {
            e.ToTable("definition_revisions", t => t.HasCheckConstraint("ck_definition_cost", "\"CreationCost\" > 0"));
            e.HasKey(x => new { x.CommunityId, x.DefinitionId, x.Revision });
            e.HasOne<DefinitionRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.DefinitionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CategoryRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CategoryId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<LetterRow>(e => {
            e.ToTable("letters"); e.HasKey(x => new { x.CommunityId, x.Id });
            e.Property(x => x.TypeId).HasDefaultValue("letter");
            e.HasOne<CategoryRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CategoryId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.AuthorCharacterId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<HoldingRow>(e => {
            e.ToTable("holdings", t => {
                t.HasCheckConstraint("ck_holding_quantity", "\"Quantity\" >= 0 AND \"Reserved\" >= 0 AND \"Reserved\" <= \"Quantity\"");
                t.HasCheckConstraint("ck_holding_payload", "(\"TypeId\" = 'potion' AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");
            });
            e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.CharacterId, x.DefinitionId, x.DefinitionRevision }).IsUnique();
            e.HasIndex(x => new { x.CommunityId, x.LetterId }).IsUnique();
            e.HasIndex(x => new { x.CommunityId, x.CharacterId, x.TypeId });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CategoryRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CategoryId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DefinitionRevisionRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.DefinitionId, x.DefinitionRevision }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LetterRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.LetterId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<QuotaRow>(e => {
            e.ToTable("quota_usage", t => t.HasCheckConstraint("ck_quota_usage", "\"Used\" >= 0 AND \"Used\" <= \"Limit\""));
            e.HasKey(x => new { x.CommunityId, x.CharacterId, x.Kind, x.PeriodStart });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<MediaAssetRow>(e => {
            e.ToTable("media_assets", t => t.HasCheckConstraint("ck_media_bounds", "(\"Purpose\" = 'currency' AND \"Width\" BETWEEN 1 AND 128 AND \"Height\" BETWEEN 1 AND 128 AND octet_length(\"Png\") <= 131072) OR (\"Purpose\" IN ('community_logo', 'group_logo') AND \"Width\" BETWEEN 1 AND 512 AND \"Height\" BETWEEN 1 AND 512 AND octet_length(\"Png\") <= 1048576)"));
            e.HasKey(x => new { x.CommunityId, x.Id });
            e.Property(x => x.Purpose).HasMaxLength(16).HasDefaultValue("currency");
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<CommunityRow>().HasOne<MediaAssetRow>().WithMany().HasForeignKey(x => new { x.Id, x.LogoAssetId }).OnDelete(DeleteBehavior.Restrict);
        m.Entity<CommunityRow>().HasOne<MediaAssetRow>().WithMany().HasForeignKey(x => new { x.Id, x.CurrencyIconAssetId }).OnDelete(DeleteBehavior.Restrict);
    }
}
