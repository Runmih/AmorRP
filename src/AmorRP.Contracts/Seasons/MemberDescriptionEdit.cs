namespace AmorRP.Contracts.Seasons;

public sealed record MemberDescriptionEdit(string Description)
{
    public const int MaximumCharacters = 300;
}
