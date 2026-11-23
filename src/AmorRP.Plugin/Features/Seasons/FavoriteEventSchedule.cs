using AmorRP.Contracts.Groups;

namespace AmorRP.Plugin.Features.Seasons;

internal static class FavoriteEventSchedule
{
    public static GroupEvent[] Upcoming(IEnumerable<GroupEvent> events, DateTimeOffset now) => events
        .Where(x => x.EndsAt > now && x.StartsAt <= now.AddDays(5))
        .OrderBy(x => x.StartsAt)
        .ThenBy(x => x.Id)
        .ToArray();

    public static string CardRange(GroupEvent item)
    {
        var start = item.StartsAt.UtcDateTime;
        var end = item.EndsAt.UtcDateTime;
        if (start.Date == end.Date)
            return FormattableString.Invariant($"{start:ddd, d MMM} · {start:HH:mm}–{end:HH:mm} ST");

        return start.Year == end.Year
            ? FormattableString.Invariant($"{start:d MMM, HH:mm} to {end:d MMM, HH:mm} ST")
            : FormattableString.Invariant($"{start:d MMM yyyy, HH:mm} to {end:d MMM yyyy, HH:mm} ST");
    }

    public static string When(GroupEvent item, DateTimeOffset now)
    {
        if (item.StartsAt <= now) return "Live now · until " + item.EndsAt.UtcDateTime.ToString("HH:mm 'ST'", System.Globalization.CultureInfo.InvariantCulture);
        var start = item.StartsAt.UtcDateTime;
        var days = (start.Date - now.UtcDateTime.Date).Days;
        var day = days switch { 0 => "Today", 1 => "Tomorrow", _ => start.ToString("ddd dd MMM", System.Globalization.CultureInfo.InvariantCulture) };
        return day + " · " + start.ToString("HH:mm 'ST'", System.Globalization.CultureInfo.InvariantCulture);
    }
}
