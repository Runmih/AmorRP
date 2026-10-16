namespace AmorRP.Contracts.Currency;

public sealed record Currency(Guid Id, Guid GroupId, string Name, string Symbol, int Version, Guid? IconAssetId = null);
