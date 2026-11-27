using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Currency;
namespace AmorRP.Contracts.Seasons;

public sealed record Member(Character Character, string Status, bool IsOwner, string[] Capabilities, bool TradingRestricted, int Version, string Etag, string Description = "", bool CombatRestricted = false, string[]? BlockedGameplayGrants = null);
