namespace AmorRP.Contracts.Groups;

public sealed record GroupPage(Group[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
