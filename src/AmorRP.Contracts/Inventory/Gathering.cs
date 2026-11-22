namespace AmorRP.Contracts.Inventory;

public sealed record SupplyPackage(string TypeId, string Name, int Revision, int Cost, int BonusPoints);
public sealed record GatheringSettings(int Version, int WeeklyPoints, int NextWeeklyPoints, DateTimeOffset ResetAt, SupplyPackage[] Packages);
public sealed record GatheringSettingsEdit(int ExpectedVersion, int WeeklyPoints, int AlchemyCost, int AlchemyBonus, int CookingCost, int CookingBonus);
public sealed record GatherSupplies(string TypeId, int ExpectedRevision, int Quantity);
public sealed record UseSupplies(int ExpectedHoldingVersion);
