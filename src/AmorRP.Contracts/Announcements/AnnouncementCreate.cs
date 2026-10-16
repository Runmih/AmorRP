namespace AmorRP.Contracts.Announcements;

public sealed record AnnouncementCreate(string Title, string Body)
{
    public const int MaximumTitleCharacters = 120;
    public const int MaximumBodyCharacters = 5000;
    public const int RetainedCount = 5;
}
