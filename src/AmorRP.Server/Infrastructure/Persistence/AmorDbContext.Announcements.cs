using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Infrastructure.Persistence;

public sealed partial class AmorDbContext
{
    public DbSet<AnnouncementRow> Announcements => Set<AnnouncementRow>();
    private static void ConfigureAnnouncements(ModelBuilder model)
    {
        model.Entity<AnnouncementRow>(e =>
        {
            e.ToTable("announcements"); e.HasKey(x => new { x.GroupId, x.Id });
            e.HasIndex(x => new { x.GroupId, x.Sequence }).IsUnique();
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CharacterRow>().WithMany().HasForeignKey(x => x.AuthorCharacterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
