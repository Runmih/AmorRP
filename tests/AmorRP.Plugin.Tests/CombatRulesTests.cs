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
    [InlineData(8, 0)]
    [InlineData(13, 5)]
    [InlineData(14, 7)]
    [InlineData(15, 9)]
    public void Point_buy_uses_the_5e_cost_table(int score, int cost) => Assert.Equal(cost, CombatRules.ScoreCost(score));

    [Theory]
    [InlineData(8, 19)]
    [InlineData(10, 20)]
    [InlineData(14, 22)]
    public void Constitution_modifies_hit_point_maximum(int constitution, int hp) =>
        Assert.Equal(hp, CombatRules.HitPointMaximum(constitution));

    [Fact]
    public void Critical_hits_double_the_number_of_dice_and_add_the_modifier_once()
    {
        Assert.Equal(4, CombatRules.DamageDiceCount(2, true));
        Assert.Equal(2, CombatRules.DamageDiceCount(2, false));
        Assert.Equal(9, CombatRules.Damage(7, 2));
        Assert.Equal(0, CombatRules.Damage(2, -3));
    }
}
