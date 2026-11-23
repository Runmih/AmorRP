namespace AmorRP.Contracts.Inventory;

public sealed record MoodleEffect(string Name, string Description, int IconId, int DurationSeconds, bool Debuff);
public sealed record MoodleGrant(Guid Id, MoodleEffect Effect, DateTimeOffset? ExpiresAt);
public sealed record MoodleGrants(MoodleGrant[] Active, Guid[] Remove, DateTimeOffset ServerTime);
public sealed record OwnershipEffectPolicy(int Version, MoodleEffect? Effect);
