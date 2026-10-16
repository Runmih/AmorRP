using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Auth;

public sealed record LoginExchangeRequest([property: JsonRequired] string PairingCode);
