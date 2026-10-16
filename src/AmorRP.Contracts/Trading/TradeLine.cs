using AmorRP.Contracts.Auth;
namespace AmorRP.Contracts.Trading;

public sealed record TradeLine(Guid HoldingId, int Quantity);
