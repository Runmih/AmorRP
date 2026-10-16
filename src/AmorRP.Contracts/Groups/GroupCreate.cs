using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record GroupCreate([property: JsonRequired] string Name, [property: JsonRequired] string Description, [property: JsonRequired] string CurrencyName, [property: JsonRequired] string CurrencySymbol, [property: JsonRequired] int WeeklyPotionPoints, [property: JsonRequired] int WeeklyLetters);
