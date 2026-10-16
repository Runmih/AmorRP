namespace AmorRP.Contracts.Inventory;

public sealed record ConsumptionRequest(int ExpectedHoldingVersion, bool ChatConsent, string Destination);
