using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record MemberCapabilities([property: JsonRequired] string[] Capabilities,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? TradingAllowed = null);
