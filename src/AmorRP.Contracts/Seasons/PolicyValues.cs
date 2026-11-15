using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Seasons;

public sealed record PolicyValues(int PotionPoints, int Letters, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int CookingPoints = 0);
