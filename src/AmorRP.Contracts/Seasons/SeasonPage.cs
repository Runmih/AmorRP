namespace AmorRP.Contracts.Seasons;

public sealed record SeasonPage(Season[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
