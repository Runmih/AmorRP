using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class MoodleGrantRow
{
    public Guid CommunityId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid Id { get; set; }
    public string EffectJson { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed partial class AmorDbContext
{
    public DbSet<MoodleGrantRow> MoodleGrants => Set<MoodleGrantRow>();

    private static void ConfigureMoodles(ModelBuilder model)
    {
        model.Entity<CommunityRow>().Property(x => x.OwnershipEffectVersion).HasDefaultValue(1);
        model.Entity<MoodleGrantRow>(e =>
        {
            e.ToTable("moodle_grants");
            e.HasKey(x => new { x.CommunityId, x.CharacterId, x.Id });
            e.HasIndex(x => new { x.CharacterId, x.ExpiresAt });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
