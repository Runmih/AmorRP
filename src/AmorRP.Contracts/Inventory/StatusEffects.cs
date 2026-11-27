namespace AmorRP.Contracts.Inventory;

public sealed record StatusEffectReference(Guid Id, int Revision);

public sealed record StatusEffect(Guid Id, int Revision, string Name, int DurationSeconds,
    string Modifier, int Amount, string? Ability, MoodleEffect? Moodle)
{
    public StatusEffectReference Reference() => new(Id, Revision);
}
