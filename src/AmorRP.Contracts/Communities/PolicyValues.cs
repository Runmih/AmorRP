using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record PolicyValues(int PotionPoints, int Letters, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int CookingPoints = 0);
