namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class AnnouncementRow
{
    public Guid GroupId { get; set; }
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public long Sequence { get; set; }
    public Guid AuthorCharacterId { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
}
