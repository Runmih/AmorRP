using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Currency;

public sealed record CurrencyAdjustment([property: JsonRequired] Guid TargetCharacterId, [property: JsonRequired] string Delta, [property: JsonRequired] string Reason);
