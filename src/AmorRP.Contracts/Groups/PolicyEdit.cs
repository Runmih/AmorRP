using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record PolicyEdit([property: JsonRequired] int PotionPoints, [property: JsonRequired] int Letters);
