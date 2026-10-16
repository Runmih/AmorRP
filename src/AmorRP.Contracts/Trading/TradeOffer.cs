using AmorRP.Contracts.Auth;
namespace AmorRP.Contracts.Trading;

public sealed record TradeOffer(Character Character, string CurrencyAmount, TradeItem[] Items, bool Confirmed, bool FinalConfirmed = false);
