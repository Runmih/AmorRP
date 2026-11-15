using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Seasons;

public sealed record SeasonCreate([property: JsonRequired] string Name, [property: JsonRequired] string Description, [property: JsonRequired] string CurrencyName, [property: JsonRequired] string CurrencySymbol, [property: JsonRequired] int WeeklyPotionPoints, [property: JsonRequired] int WeeklyLetters, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int WeeklyCookingPoints = 0);
