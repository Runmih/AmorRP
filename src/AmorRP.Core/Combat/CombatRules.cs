namespace AmorRP.Core.Combat;

public static class CombatRules
{
    public static IReadOnlyList<string> Abilities { get; } = Array.AsReadOnly(new[] { "STR", "DEX", "CON", "INT", "WIS", "CHA" });
    public const int BaseScore = 8;
    public const int PointBuyMaximum = 15;
    public const int MaximumScore = 20;
    public const int BaseHitPoints = 20;
    public const int ConstitutionIndex = 2;
    public static int StartingHitPoints => HitPointMaximum(BaseScore);
    public static int HighestHitPoints => HitPointMaximum(MaximumScore);
    public const int WeaponProficiency = 2;
    public const int DefaultStatPoints = 27;
    public const int MaximumBonusPoints = 60;
    public const string BlacksmithingQuota = "blacksmithing_points";

    public static int AbilityModifier(int score) => (int)Math.Floor((score - 10) / 2d);
    public static bool Hits(int die, int total, int armorClass) => die == 20 || (die != 1 && total >= armorClass);
    public static int ScoreCost(int score) => score <= 13 ? score - BaseScore : 5 + (score - 13) * 2;
    public static int AllocationCost(int score, int increase) => ScoreCost(score + increase) - ScoreCost(score);
    public static int HitPointMaximum(int constitution) => BaseHitPoints + AbilityModifier(constitution);
    public static int DamageDiceCount(int count, bool critical) => critical ? count * 2 : count;
    public static int Damage(int rolledDamage, int modifier) => Math.Max(0, rolledDamage + modifier);
}
