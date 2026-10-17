using System.Text.Json.Serialization;
namespace AmorRP.Contracts.Communities;

public sealed record CommunityEdit([property: JsonRequired] string Name, [property: JsonRequired] string Description)
{
    public const int MaximumDescriptionCharacters = 300;
}
