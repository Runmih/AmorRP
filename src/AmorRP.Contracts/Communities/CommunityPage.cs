namespace AmorRP.Contracts.Communities;

public sealed record CommunityPage(Community[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
