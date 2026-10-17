namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class LetterRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public Guid AuthorCharacterId { get; set; }
    public Guid CategoryId { get; set; }
    public string TypeId { get; set; } = "letter";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTimeOffset? FirstTradedAt { get; set; }
}
