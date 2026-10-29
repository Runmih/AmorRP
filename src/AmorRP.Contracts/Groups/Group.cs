namespace AmorRP.Contracts.Groups;

public sealed record Group(Guid Id, Guid CommunityId, string Name, string Description, Guid? LogoAssetId, int LogoVersion, int Version, string Etag, Guid OwnerCharacterId = default, string JoinMode = "open", bool IsMember = false, bool IsFavorite = false, string[]? EnabledFeatures = null, string[]? MyCapabilities = null);
public sealed record GroupWrite(string Name, string Description, string JoinMode = "open", string? Password = null);
public sealed record GroupPage(Group[] Items, string? NextCursor);
public sealed record GroupJoin(string? Password = null);
public sealed record GroupNotice(Guid GroupId, string GroupName, string Title, string Message, string Reward, int Version);
public sealed record NoticeWrite(string Title, string Message, string Reward);
public sealed record GroupEvent(Guid Id, Guid CommunityId, Guid GroupId, string GroupName, string Title, string Description, string Location, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Version);
public sealed record EventWrite(string Title, string Description, string Location, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record GroupAnnouncement(Guid Id, string Title, string Message, DateTimeOffset PublishedAt);
public sealed record AnnouncementWrite(string Title, string Message);
public sealed record GroupMember(Guid CharacterId, string Name, string World, bool IsOwner, string[]? Capabilities = null, string? Etag = null);
public sealed record GroupPrivate(GroupMember[] Members, GroupAnnouncement[] Announcements, string? NextCursor = null);
public sealed record GroupHub(GroupNotice[] Notices, GroupEvent[] Events);
public sealed record FavoriteEvents(GroupEvent[] Items);
