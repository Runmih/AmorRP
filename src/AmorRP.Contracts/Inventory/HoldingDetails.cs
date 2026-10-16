namespace AmorRP.Contracts.Inventory;

public sealed record HoldingDetails(Holding Holding, Definition? Definition = null, Letter? Letter = null);
