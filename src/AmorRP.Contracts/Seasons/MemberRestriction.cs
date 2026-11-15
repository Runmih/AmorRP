using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Seasons;

public sealed record MemberRestriction([property: JsonRequired] bool Restricted, [property: JsonRequired] string Reason);
