using AmorRP.Contracts.Auth;
namespace AmorRP.Contracts.Trading;

public sealed record TradeItem(Guid HoldingId, int Quantity, string TypeId, string Name,
    string? Description, string? UseMessage, Character? Author, Guid? DefinitionId = null, int? DefinitionRevision = null);
