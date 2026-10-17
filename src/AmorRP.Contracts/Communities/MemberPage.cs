namespace AmorRP.Contracts.Communities;

public sealed record MemberPage(Member[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
