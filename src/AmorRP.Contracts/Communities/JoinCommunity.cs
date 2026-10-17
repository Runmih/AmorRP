using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record JoinCommunity([property: JsonRequired] string InvitationCode, [property: JsonRequired] bool AcceptMemberVisibility);
