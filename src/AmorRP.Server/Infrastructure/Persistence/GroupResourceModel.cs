using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class GroupStockRow
{
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DefinitionId { get; set; }
    public int Revision { get; set; }
    public int Quantity { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class GroupShiftRow
{
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CharacterId { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
}

public sealed partial class AmorDbContext
{
    public DbSet<GroupStockRow> GroupStock => Set<GroupStockRow>();
    public DbSet<GroupShiftRow> GroupShifts => Set<GroupShiftRow>();

    private static void ConfigureGroupResources(ModelBuilder model)
    {
        model.Entity<GroupStockRow>(e =>
        {
            e.ToTable("group_stock", t => t.HasCheckConstraint("ck_group_stock_positive", "\"Quantity\" > 0"));
            e.HasKey(x => new { x.CommunityId, x.GroupId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.GroupId, x.DefinitionId, x.Revision }).IsUnique();
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DefinitionRevisionRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.DefinitionId, x.Revision }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<GroupShiftRow>(e =>
        {
            e.ToTable("group_shifts", t => t.HasCheckConstraint("ck_group_shift_range", "\"End\" > \"Start\""));
            e.HasKey(x => new { x.CommunityId, x.GroupId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.GroupId, x.CharacterId, x.End });
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
