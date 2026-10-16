using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record MemberRestriction([property: JsonRequired] bool Restricted, [property: JsonRequired] string Reason);
