using AmorRP.Contracts.Seasons;

namespace AmorRP.Plugin.Services.Api;

internal static class SeasonSelection
{
    public static bool CanCreate(SeasonContext? context) => context?.MyCapabilities?.Contains(SeasonCapabilities.Create) == true;
    // Discovery and the membership list are separate reads. Prefer the active state
    // from the list we can actually display so a rollover between reads is not lost.
    public static Guid? ObservedActive(IReadOnlyList<Season> seasons, Guid? discovered) =>
        seasons.FirstOrDefault(x => x.SeasonState == "active")?.Id ?? discovered;

    public static Season? Choose(IReadOnlyList<Season> seasons, Guid? selected, Guid? active, Guid? previouslyActive, bool observed)
    {
        if (active.HasValue && (!observed || active != previouslyActive))
            return seasons.FirstOrDefault(x => x.Id == active) ?? seasons.FirstOrDefault(x => x.Id == selected) ?? seasons.FirstOrDefault();
        return seasons.FirstOrDefault(x => x.Id == selected) ?? seasons.FirstOrDefault(x => x.Id == active) ?? seasons.FirstOrDefault();
    }

    public static bool CanSubmit(Season? season, CommandRequest command) =>
        !ReadOnly(season) || !command.Path.StartsWith("api/v2/seasons/", StringComparison.Ordinal)
        || command.Operation.Kind is CommandKind.ActivateSeason or CommandKind.ArchiveSeason or CommandKind.SetSeasonCreationPermission;

    public static bool ReadOnly(Season? season) => season != null && season.SeasonState != "active";
}
