using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Currency;
namespace AmorRP.Contracts.Groups;

public sealed record Member(Character Character, string Status, bool IsOwner, string[] Capabilities, bool TradingRestricted, int Version, string Etag, string Description = "");
