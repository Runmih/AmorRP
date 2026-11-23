using System.Text.Json.Serialization;
using AmorRP.Contracts.Inventory;
namespace AmorRP.Contracts.Seasons;

public sealed record PolicyEdit([property: JsonRequired] int PotionPoints, [property: JsonRequired] int Letters, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int CookingPoints = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GatheringSettingsEdit? Gathering = null);
