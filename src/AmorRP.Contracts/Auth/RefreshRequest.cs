using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Auth;
public sealed record RefreshRequest([property: JsonRequired] string RefreshToken);
