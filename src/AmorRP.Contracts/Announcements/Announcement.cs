using AmorRP.Contracts.Auth;
namespace AmorRP.Contracts.Announcements;

public sealed record Announcement(Guid Id, Guid CommunityId, string Title, string Body, Character Author, DateTimeOffset PublishedAt);
public sealed record AnnouncementSummary(Guid Id, string Title, DateTimeOffset PublishedAt, bool Unread);
public sealed record AnnouncementPage(AnnouncementSummary[] Items);
public sealed record AnnouncementReadResult(Guid AnnouncementId, int UnreadCount);
public sealed record CommunityAnnouncementUnread(Guid CommunityId, int UnreadCount);
public sealed record AnnouncementUnreadPage(CommunityAnnouncementUnread[] Communities);
