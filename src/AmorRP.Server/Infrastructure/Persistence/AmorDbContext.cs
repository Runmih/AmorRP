using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext(DbContextOptions<AmorDbContext> options) : DbContext(options)
{
    public DbSet<InfrastructureState> InfrastructureStates => Set<InfrastructureState>();

    public DbSet<CharacterRow> Characters => Set<CharacterRow>();
    public DbSet<LoginRow> Logins => Set<LoginRow>();
    public DbSet<SessionRow> Sessions => Set<SessionRow>();
    public DbSet<UsedRefreshRow> UsedRefreshTokens => Set<UsedRefreshRow>();
    public DbSet<GroupRow> Groups => Set<GroupRow>();
    public DbSet<CommunityRow> Communities => Set<CommunityRow>();
    public DbSet<MembershipRow> Memberships => Set<MembershipRow>();
    public DbSet<InvitationRow> Invitations => Set<InvitationRow>();
    public DbSet<OwnershipRow> OwnershipTransfers => Set<OwnershipRow>();
    public DbSet<OperationRow> Operations => Set<OperationRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GroupRow>(e => {
            e.Property(x => x.EnabledFeatures).HasDefaultValue(AmorRP.Contracts.Groups.GroupCapabilities.All.ToArray());
            e.ToTable("groups"); e.HasKey(x => new { x.CommunityId, x.Id });
            e.Property(x => x.Name).HasMaxLength(80); e.Property(x => x.Description).HasColumnType("text");
            e.HasIndex(x => new { x.OwnerCharacterId, x.Deleted });
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.OwnerCharacterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MediaAssetRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.LogoAssetId }).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureGroupActivity(modelBuilder);
        ConfigureAnnouncements(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureTrading(modelBuilder);
        modelBuilder.Entity<CharacterRow>(e => {
            e.ToTable("characters"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.LodestoneId).IsUnique(); e.Property(x => x.LodestoneId).HasMaxLength(20);
            e.Property(x => x.OwnershipKeyHash).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(80); e.Property(x => x.HomeWorldName).HasMaxLength(80);
        });
        modelBuilder.Entity<LoginRow>(e => {
            e.ToTable("login_attempts"); e.HasKey(x => x.Id); e.HasIndex(x => x.StateHash).IsUnique();
            e.HasIndex(x => x.ExpiresAt); e.Property(x => x.CredentialHash).HasMaxLength(64);
            e.Property(x => x.PairingCodeHash).HasMaxLength(64);
            e.Property(x => x.OriginHash).HasMaxLength(64);
            e.HasIndex(x => new { x.OriginHash, x.ExpiresAt });
            e.HasIndex(x => new { x.OriginHash, x.StartedAt });
        });
        modelBuilder.Entity<SessionRow>(e => {
            e.ToTable("sessions"); e.HasKey(x => x.Id); e.HasIndex(x => x.AccessHash).IsUnique();
            e.HasIndex(x => x.RefreshHash).IsUnique(); e.HasIndex(x => new { x.CharacterId, x.CreatedAt });
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<UsedRefreshRow>(e => {
            e.ToTable("used_refresh_tokens"); e.HasKey(x => x.TokenHash); e.HasIndex(x => x.ExpiresAt);
            e.HasOne<SessionRow>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CommunityRow>(e => {
            e.ToTable("communities", t => t.HasCheckConstraint("ck_community_policy", "\"CurrentPotionPoints\" >= 0 AND \"CurrentLetters\" >= 0 AND \"NextPotionPoints\" >= 0 AND \"NextLetters\" >= 0"));
            e.HasKey(x => x.Id); e.HasIndex(x => x.CurrencyId).IsUnique();
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.CreatedByCharacterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.OwnerCharacterId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasMaxLength(80); e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.LogoVersion).HasDefaultValue(1);
            e.Property(x => x.CurrencyName).HasMaxLength(80); e.Property(x => x.CurrencySymbol).HasMaxLength(16);
        });
        modelBuilder.Entity<MembershipRow>(e => {
            e.ToTable("memberships", t => {
                t.HasCheckConstraint("ck_membership_balance", "\"Owned\" >= 0 AND \"Reserved\" >= 0 AND \"Reserved\" <= \"Owned\"");
                t.HasCheckConstraint("ck_membership_status", "\"Status\" IN ('active', 'dormant', 'blocked')");
            });
            e.HasKey(x => new { x.CommunityId, x.CharacterId }); e.HasIndex(x => new { x.CharacterId, x.Status });
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<InvitationRow>(e => {
            e.ToTable("invitations", t => t.HasCheckConstraint("ck_invitation_uses", "\"Used\" >= 0 AND \"Used\" <= \"MaxUses\""));
            e.HasKey(x => x.Id); e.HasIndex(x => x.CodeHash).IsUnique(); e.HasIndex(x => x.CommunityId);
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OwnershipRow>(e => {
            e.ToTable("ownership_transfers"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.CommunityId, x.Status });
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.ToCharacterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.FromCharacterId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OperationRow>(e => {
            e.ToTable("operations"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.Scope, x.Key }).IsUnique();
            e.HasIndex(x => new { x.CommunityId, x.OccurredAt, x.Id }); e.HasIndex(x => x.OccurredAt);
            e.Property(x => x.RequestHash).HasMaxLength(64);
        });
        modelBuilder.Entity<InfrastructureState>(entity =>
        {
            entity.ToTable("infrastructure_state");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasMaxLength(40);
            entity.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            entity.HasData(new InfrastructureState { Id = "bootstrap", SchemaVersion = 1 });
        });
    }
}

public sealed class InfrastructureState
{
    public required string Id { get; set; }
    public int SchemaVersion { get; set; }
}
