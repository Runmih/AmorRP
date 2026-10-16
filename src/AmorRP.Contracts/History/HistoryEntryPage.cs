namespace AmorRP.Contracts.History;

public sealed record HistoryEntryPage(HistoryEntry[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
