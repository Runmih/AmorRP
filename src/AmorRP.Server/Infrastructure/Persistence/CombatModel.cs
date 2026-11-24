using AmorRP.Core.Combat;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class CombatSheetRow
{
    public Guid CommunityId { get; set; }
    public Guid CharacterId { get; set; }
    public int Version { get; set; } = 1;
    public int[] Allocated { get; set; } = new int[6];
    public int BonusPoints { get; set; }
    public int HitPoints { get; set; } = CombatRules.MaxHitPoints;
    public Guid? WeaponId { get; set; }
    public Guid? ArmorId { get; set; }
    public int ResetPotions { get; set; }
    public int ResetPurchases { get; set; }
}

public sealed partial class AmorDbContext
{
    public DbSet<CombatSheetRow> CombatSheets => Set<CombatSheetRow>();

    private static void ConfigureCombat(ModelBuilder model)
    {
        var season = model.Entity<CommunityRow>();
        season.Property(x => x.StatPointAllowance).HasDefaultValue(CombatRules.DefaultStatPoints).HasSentinel(-1);
        season.Property(x => x.BlacksmithingPoints).HasDefaultValue(10).HasSentinel(-1);
        season.Property(x => x.CombatPolicyVersion).HasDefaultValue(1);
        season.Property(x => x.ResetPotionLimit).HasDefaultValue(1).HasSentinel(-1);
        season.Property(x => x.ResetPotionVersion).HasDefaultValue(1);
        model.Entity<MembershipRow>().Property(x => x.CombatRestricted).HasDefaultValue(false);
        model.Entity<CombatSheetRow>(e =>
        {
            e.ToTable("combat_sheets", checks =>
            {
                checks.HasCheckConstraint("ck_combat_hp", "\"HitPoints\" BETWEEN 0 AND 20");
                checks.HasCheckConstraint("ck_combat_allocated", "cardinality(\"Allocated\") = 6 AND 0 <= ALL(\"Allocated\") AND 10 >= ALL(\"Allocated\")");
                checks.HasCheckConstraint("ck_combat_points_and_potions", "\"BonusPoints\" BETWEEN 0 AND 60 AND \"ResetPurchases\" >= \"ResetPotions\" AND \"ResetPotions\" >= 0");
            });
            e.HasKey(x => new { x.CommunityId, x.CharacterId });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
