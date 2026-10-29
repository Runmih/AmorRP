namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class GroupMembershipRow
{
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CharacterId { get; set; }
    public string[] Capabilities { get; set; } = [];
    public Guid PermissionVersion { get; set; } = Guid.CreateVersion7();
}
public sealed class GroupFavoriteRow
{
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CharacterId { get; set; }
}
public sealed class GroupNoticeRow
{
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Reward { get; set; } = "";
    public int Version { get; set; } = 1;
}
public sealed class GroupEventRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class GroupAnnouncementRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; }
}
