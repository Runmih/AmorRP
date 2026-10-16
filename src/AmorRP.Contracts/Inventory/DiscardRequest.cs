namespace AmorRP.Contracts.Inventory;

public sealed record DiscardRequest(int Quantity, int ExpectedHoldingVersion);
