namespace AmorRP.Contracts.Inventory;

public sealed record DefinitionWrite(string TypeId, string Name, Guid CategoryId, string Description, bool Enabled, PotionData Potion);
