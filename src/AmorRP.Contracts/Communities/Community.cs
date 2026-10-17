using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Currency;
namespace AmorRP.Contracts.Communities;

public sealed record Community(Guid Id, string Name, string Description, Guid OwnerCharacterId, int Version, AmorRP.Contracts.Currency.Currency Currency, string[] MyCapabilities, bool MyTradingRestricted, Balance MyBalance, Guid? PendingOwnershipTransferId, Guid? LogoAssetId = null, int LogoVersion = 1);
