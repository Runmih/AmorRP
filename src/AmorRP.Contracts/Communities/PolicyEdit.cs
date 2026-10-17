using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record PolicyEdit([property: JsonRequired] int PotionPoints, [property: JsonRequired] int Letters, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int CookingPoints = 0);
