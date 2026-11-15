namespace AmorRP.Contracts.Seasons;

public sealed record MemberPage(Member[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
