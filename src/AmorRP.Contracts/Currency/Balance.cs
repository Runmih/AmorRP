namespace AmorRP.Contracts.Currency;

public sealed record Balance(Guid CurrencyId, Guid CharacterId, string Owned, string Reserved, string Available, int Version);
