namespace AmorRP.Contracts.Inventory;

public sealed record Category(Guid Id, Guid GroupId, string Name, string[] AllowedTypeIds, bool Retired, int Version);
