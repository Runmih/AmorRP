using AmorRP.Contracts.Auth;
namespace AmorRP.Contracts.Trading;

public sealed record Trade(Guid Id, Guid CommunityId, Guid CurrencyId, string State, int Revision,
    DateTimeOffset ExpiresAt, TradeOffer Initiator, TradeOffer Recipient, string? EndReason,
    Guid? CompletionOperationId);
