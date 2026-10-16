using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Auth;

public sealed record LoginStart([property: JsonRequired] string DisplayName, [property: JsonRequired] string HomeWorldId, [property: JsonRequired] string HomeWorldName, Guid? KnownCharacterId = null);
