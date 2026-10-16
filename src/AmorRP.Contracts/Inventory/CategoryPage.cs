namespace AmorRP.Contracts.Inventory;

public sealed record CategoryPage(Category[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor = null);
