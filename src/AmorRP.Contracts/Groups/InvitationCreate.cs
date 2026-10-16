using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Groups;

public sealed record InvitationCreate([property: JsonRequired] int ExpiresInHours, [property: JsonRequired] int MaxUses);
