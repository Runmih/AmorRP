namespace AmorRP.Contracts.Inventory;

public sealed record Category(Guid Id, Guid CommunityId, string Name, string[] AllowedTypeIds, bool Retired, int Version);
