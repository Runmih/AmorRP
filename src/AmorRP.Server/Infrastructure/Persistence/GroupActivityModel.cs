using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext
{
    public DbSet<GroupMembershipRow> GroupMemberships => Set<GroupMembershipRow>();
    public DbSet<GroupFavoriteRow> GroupFavorites => Set<GroupFavoriteRow>();
    public DbSet<GroupNoticeRow> GroupNotices => Set<GroupNoticeRow>();
    public DbSet<GroupEventRow> GroupEvents => Set<GroupEventRow>();
    public DbSet<GroupAnnouncementRow> GroupAnnouncements => Set<GroupAnnouncementRow>();
    private static void ConfigureGroupActivity(ModelBuilder m)
    {
        m.Entity<GroupMembershipRow>(e => {
            e.ToTable("group_memberships"); e.HasKey(x => new { x.CommunityId, x.GroupId, x.CharacterId });
            e.Property(x => x.Capabilities).HasDefaultValue(Array.Empty<string>());
            e.Property(x => x.PermissionVersion).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<GroupFavoriteRow>(e => {
            e.ToTable("group_favorites"); e.HasKey(x => new { x.CommunityId, x.GroupId, x.CharacterId });
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<GroupNoticeRow>(e => {
            e.ToTable("group_notices"); e.HasKey(x => new { x.CommunityId, x.GroupId });
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<GroupEventRow>(e => {
            e.ToTable("group_events", t => t.HasCheckConstraint("ck_group_event_range", "\"EndsAt\" > \"StartsAt\""));
            e.HasKey(x => new { x.CommunityId, x.Id }); e.HasIndex(x => new { x.CommunityId, x.GroupId, x.EndsAt });
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<GroupAnnouncementRow>(e => {
            e.ToTable("group_announcements"); e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.GroupId, x.PublishedAt });
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
