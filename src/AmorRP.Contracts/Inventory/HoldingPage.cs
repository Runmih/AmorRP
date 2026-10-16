namespace AmorRP.Contracts.Inventory;

public sealed record HoldingPage(Holding[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor = null);
