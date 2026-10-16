using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Currency;

public sealed record CurrencyEdit([property: JsonRequired] string Name, [property: JsonRequired] string Symbol);
