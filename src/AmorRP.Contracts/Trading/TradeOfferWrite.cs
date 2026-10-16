using AmorRP.Contracts.Auth;
namespace AmorRP.Contracts.Trading;

public sealed record TradeOfferWrite(int ExpectedTradeRevision, Guid CurrencyId, string CurrencyAmount, TradeLine[] Items);
