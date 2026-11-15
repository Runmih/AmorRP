using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Seasons;

public sealed record MemberRemoval([property: JsonRequired] string Reason);
