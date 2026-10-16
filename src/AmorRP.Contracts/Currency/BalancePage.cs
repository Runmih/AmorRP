namespace AmorRP.Contracts.Currency;

public sealed record BalancePage(Balance[] Items, string? NextCursor, string SnapshotVersion, string? EventCursor);
