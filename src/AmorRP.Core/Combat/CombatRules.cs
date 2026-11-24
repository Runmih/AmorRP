namespace AmorRP.Core.Combat;

public static class CombatRules
{
    public static IReadOnlyList<string> Abilities { get; } = Array.AsReadOnly(new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" });
    public const int BaseScore = 10;
    public const int MaximumScore = 20;
    public const int MaxHitPoints = 20;
    public const int WeaponProficiency = 2;
    public const int DefaultStatPoints = 6;
    public const int MaximumBonusPoints = 60;
    public const string BlacksmithingQuota = "blacksmithing_points";

    public static int AbilityModifier(int score) => (int)Math.Floor((score - 10) / 2d);
    public static bool Hits(int die, int total, int armorClass) => die == 20 || (die != 1 && total >= armorClass);
    public static int Damage(int power, int modifier, bool critical) => Math.Max(1, power * (critical ? 2 : 1) + modifier);
}
