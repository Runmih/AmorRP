namespace AmorRP.Contracts.Groups;

public sealed record MemberPage(Member[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
