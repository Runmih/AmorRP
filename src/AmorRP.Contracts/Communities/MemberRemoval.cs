using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record MemberRemoval([property: JsonRequired] string Reason);
