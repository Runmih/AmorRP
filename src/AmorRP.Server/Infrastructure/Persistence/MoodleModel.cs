using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class StatusEffectRevisionRow
{
    public Guid CommunityId { get; set; }
    public Guid Id { get; set; }
    public int Revision { get; set; }
    public string EffectJson { get; set; } = "";
}

public sealed class StatusEffectGrantRow
{
    public Guid CommunityId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid Id { get; set; }
    public Guid StatusEffectId { get; set; }
    public string EffectJson { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed partial class AmorDbContext
{
    public DbSet<StatusEffectGrantRow> StatusGrants => Set<StatusEffectGrantRow>();
    public DbSet<StatusEffectRevisionRow> StatusEffectRevisions => Set<StatusEffectRevisionRow>();

    private static void ConfigureMoodles(ModelBuilder model)
    {
        model.Entity<CommunityRow>().Property(x => x.OwnershipEffectVersion).HasDefaultValue(1);
        model.Entity<StatusEffectRevisionRow>(e =>
        {
            e.ToTable("status_effect_revisions");
            e.HasKey(x => new { x.CommunityId, x.Id, x.Revision });
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StatusEffectGrantRow>(e =>
        {
            e.ToTable("moodle_grants");
            e.HasKey(x => new { x.CommunityId, x.CharacterId, x.Id });
            e.HasIndex(x => new { x.CharacterId, x.ExpiresAt });
            e.HasIndex(x => new { x.CommunityId, x.CharacterId, x.StatusEffectId }).IsUnique();
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
