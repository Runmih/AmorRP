using AmorRP.Contracts.Communities;

namespace AmorRP.Plugin.Services.Api;

internal static class SeasonSelection
{
    public static Community? Choose(IReadOnlyList<Community> seasons, Guid? selected, Guid? active, Guid? previouslyActive, bool observed)
    {
        if (active.HasValue && (!observed || active != previouslyActive))
            return seasons.FirstOrDefault(x => x.Id == active) ?? seasons.FirstOrDefault(x => x.Id == selected) ?? seasons.FirstOrDefault();
        return seasons.FirstOrDefault(x => x.Id == selected) ?? seasons.FirstOrDefault(x => x.Id == active) ?? seasons.FirstOrDefault();
    }

    public static bool ReadOnly(bool enabled, Community? season) => season != null &&
        (season.SeasonState == "archived" || enabled && season.SeasonState != "active");
}
