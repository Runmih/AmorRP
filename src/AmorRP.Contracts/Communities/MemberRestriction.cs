using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record MemberRestriction([property: JsonRequired] bool Restricted, [property: JsonRequired] string Reason);
