using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Seasons;

public sealed record SeasonEdit([property: JsonRequired] string Name, [property: JsonRequired] string Description)
{
    public const int MaximumDescriptionCharacters = 300;
}
