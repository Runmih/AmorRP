namespace AmorRP.Contracts.Inventory;

public sealed record HoldingChange(int ExpectedHoldingVersion, int RemainingQuantity, string? Reason = null);
public sealed record HoldingDeletion(int ExpectedHoldingVersion, string? Reason = null);
