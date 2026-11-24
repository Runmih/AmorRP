using AmorRP.Core.Combat;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class CombatRulesTests
{
    [Theory]
    [InlineData(1, 20, 12, false)]
    [InlineData(20, 20, 25, true)]
    [InlineData(13, 16, 16, true)]
    [InlineData(13, 16, 17, false)]
    public void Attack_rules_include_natural_rolls_and_ties(int die, int total, int armorClass, bool hit) =>
        Assert.Equal(hit, CombatRules.Hits(die, total, armorClass));

    [Theory]
    [InlineData(3, 2, false, 5)]
    [InlineData(3, 2, true, 8)]
    [InlineData(2, -3, false, 1)]
    public void Critical_hits_double_weapon_power_before_applying_the_modifier(int power, int modifier, bool critical, int damage) =>
        Assert.Equal(damage, CombatRules.Damage(power, modifier, critical));
}
