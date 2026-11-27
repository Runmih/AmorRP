using AmorRP.Contracts.Inventory;

namespace AmorRP.Contracts.Combat;

public sealed record CombatSkill(Guid Id, string Name, string Description, string Kind, string Icon,
    string Target, string Resolution, string RollAbility, int SaveDc, string Effect, int DiceCount, int DiceSides, string? EffectAbility, StatusEffectReference? StatusEffect = null);
public sealed record SkillLibrary(int Version, CombatSkill[] Skills);
public sealed record SkillUse(Guid SkillId, int LibraryVersion, Guid? TargetId, string Destination = "");
public sealed record SkillResult(string Skill, string Actor, string Target, string Effect, bool Applied, CombatRoll? Roll,
    int? Difficulty, int RolledEffect, int EffectModifier, int Amount, int RemainingHp, bool StayedStanding, string? AppliedStatus = null);
