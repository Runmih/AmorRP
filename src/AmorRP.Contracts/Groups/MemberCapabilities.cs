using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record MemberCapabilities([property: JsonRequired] string[] Capabilities,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? TradingAllowed = null);
