namespace AmorRP.Contracts.Inventory;

public sealed record Definition(Guid Id, Guid CommunityId, string TypeId, int Revision, int TypeDataVersion, string Name, Guid CategoryId, string Description, bool Enabled, bool Retired, PotionData Potion);
