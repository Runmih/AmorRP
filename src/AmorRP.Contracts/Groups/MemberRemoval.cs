using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record MemberRemoval([property: JsonRequired] string Reason);
