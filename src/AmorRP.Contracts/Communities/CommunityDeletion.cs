using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record CommunityDeletion([property: JsonRequired] string ConfirmName);
