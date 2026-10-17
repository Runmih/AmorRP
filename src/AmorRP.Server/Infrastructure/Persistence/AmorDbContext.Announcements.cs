using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext
{
    public DbSet<AnnouncementRow> Announcements => Set<AnnouncementRow>();
    private static void ConfigureAnnouncements(ModelBuilder model)
    {
        model.Entity<AnnouncementRow>(e =>
        {
            e.ToTable("announcements"); e.HasKey(x => new { x.CommunityId, x.Id });
            e.HasIndex(x => new { x.CommunityId, x.Sequence }).IsUnique();
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.AuthorCharacterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
