namespace AmorRP.Contracts.Inventory;

public sealed record PotionCreate(Guid DefinitionId, int ExpectedDefinitionRevision, int Quantity);
