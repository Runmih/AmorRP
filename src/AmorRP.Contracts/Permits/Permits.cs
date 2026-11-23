namespace AmorRP.Contracts.Permits;

public sealed record PermitPolicy(string BasePrice, bool Enabled);
public sealed record PermitListingEdit(Guid CharacterId);
public sealed record PermitListing(Guid CharacterId, string CharacterName, string Status, string Total, string Etag);
public sealed record PermitListingConsent(bool Publish, string? ExpectedTotal = null);
public sealed record PermitPurchase(Guid CharacterId, Guid GroupId, string ExpectedTotal);
public sealed record PermitOfferCreate(Guid CharacterId, Guid GroupId, string ExpectedBasePrice);
public sealed record PermitQuoteAction(string Quote);
public sealed record PermitAsset(Guid HoldingId, string Name, int Quantity, int Version);
public sealed record PermitOffer(Guid Id, Guid CharacterId, string CharacterName, Guid BuyerId, string BuyerName,
    Guid GroupId, string GroupName, bool Private, string Status, DateTimeOffset ExpiresAt, string Quote,
    string BasePrice, string Debt, string Total, string AssetsCurrency, PermitAsset[] Assets);
public sealed record OwnershipPermit(Guid Id, Guid CharacterId, string CharacterName, Guid GroupId, string GroupName, DateTimeOffset RegisteredAt, DateTimeOffset? ReleasedAt);
public sealed record PermitShop(PermitPolicy Policy, PermitListing[] Listings, PermitOffer[] Offers, OwnershipPermit[] Permits);
