namespace AmorRP.Contracts.Inventory;

public sealed record DefinitionPage(Definition[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor = null);
