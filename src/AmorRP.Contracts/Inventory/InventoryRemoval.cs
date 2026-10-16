namespace AmorRP.Contracts.Inventory;

public sealed record InventoryRemoval(Guid TargetCharacterId, Guid HoldingId, int Quantity, int ExpectedHoldingVersion, string Reason);
