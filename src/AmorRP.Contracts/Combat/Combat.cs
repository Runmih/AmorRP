using AmorRP.Contracts.Inventory;

namespace AmorRP.Contracts.Combat;

public sealed record CombatGear(string Form, string? Ability, int Value);
public sealed record EquippedItem(Guid HoldingId, string Name, CombatGear Gear);
public sealed record CombatSheet(int Version, int[] Scores, int AvailablePoints, int BonusPoints, int HitPoints, int MaxHitPoints,
    int ArmorClass, EquippedItem? Weapon, EquippedItem? Armor, bool CanAttack, int ResetPotions);
public sealed record CombatPolicy(int Version, int StatPoints, int BlacksmithingPoints, MoodleEffect? DownedEffect);
public sealed record CombatAllocation(int Version, int[] Points);
public sealed record CombatEquip(int Version, string Slot, Guid? HoldingId);
public sealed record CombatAttack(Guid TargetId, string Destination = "");
public sealed record CombatCheck(string Ability, string Destination = "");
public sealed record CombatRoll(string Ability, int Die, int Modifier, int Proficiency, int Total);
public sealed record CombatHit(string Attacker, string Target, string Weapon, CombatRoll Roll, int ArmorClass,
    bool Hit, bool Critical, int Damage, int RemainingHp, bool Downed);
public sealed record CombatHealthEdit(int HitPoints);
public sealed record ResetPotionProduct(int Version, long Price, int PerCharacterLimit, int Purchased, int Owned, string? Name = "Character Reset Potion");
public sealed record ResetPotionSettings(int Version, long Price, int PerCharacterLimit);
public sealed record ResetPotionBuy(int Version);
