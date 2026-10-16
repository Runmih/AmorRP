namespace AmorRP.Contracts.Inventory;

public sealed record ConsumptionResult(Holding Holding, string Message, string Destination, string ChatDelivery = "client_action_required");
