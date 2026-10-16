using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record JoinGroup([property: JsonRequired] string InvitationCode, [property: JsonRequired] bool AcceptMemberVisibility);
