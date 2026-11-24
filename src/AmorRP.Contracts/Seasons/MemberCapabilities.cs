using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Seasons;

public sealed record MemberCapabilities([property: JsonRequired] string[] Capabilities,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? TradingAllowed = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? CombatAllowed = null);
