using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Features.Communities;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class FavoriteEventScheduleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T23:30:00Z");
    private static GroupEvent Event(DateTimeOffset start, DateTimeOffset end) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Patrol", "Meetup", "Evening meetup", "Gridania", start, end, 1);

    [Fact]
    public void The_five_day_window_includes_ongoing_and_boundary_events_but_excludes_finished_and_later_events()
    {
        var live = Event(Now.AddHours(-2), Now.AddMinutes(1));
        var upcoming = Event(Now.AddDays(2), Now.AddDays(2).AddHours(1));
        var boundary = Event(Now.AddDays(5), Now.AddDays(5).AddHours(1));
        var beyond = Event(Now.AddDays(5).AddTicks(1), Now.AddDays(6));
        var ended = Event(Now.AddHours(-1), Now);

        var visible = FavoriteEventSchedule.Upcoming([beyond, ended, boundary, upcoming, live], Now);

        Assert.Equal(new[] { live.Id, upcoming.Id, boundary.Id }, visible.Select(x => x.Id));
        Assert.Empty(FavoriteEventSchedule.Upcoming([live], live.EndsAt));
    }

    [Fact]
    public void Time_labels_use_server_time_even_when_the_supplied_offsets_cross_a_calendar_day()
    {
        var tomorrow = Event(DateTimeOffset.Parse("2026-10-19T10:00:00+09:00"), Now.AddHours(4));
        var sameDay = Event(Now.AddMinutes(10), Now.AddMinutes(20));

        Assert.Equal("Tomorrow · 01:00 ST", FavoriteEventSchedule.When(tomorrow, Now.ToOffset(TimeSpan.FromHours(9))));
        Assert.Equal("Today · 23:40 ST", FavoriteEventSchedule.When(sameDay, Now));
        Assert.StartsWith("Live now", FavoriteEventSchedule.When(sameDay, sameDay.StartsAt));
    }

    [Fact]
    public void Rolling_five_day_filtering_compares_instants_and_moves_forward_without_another_server_request()
    {
        var eventStart = Now.AddDays(5).AddHours(1).ToOffset(TimeSpan.FromHours(-7));
        var item = Event(eventStart, eventStart.AddHours(1));
        Assert.Empty(FavoriteEventSchedule.Upcoming([item], Now));
        Assert.Single(FavoriteEventSchedule.Upcoming([item], Now.AddHours(1)));
        Assert.Empty(FavoriteEventSchedule.Upcoming([item], item.EndsAt));
    }
}
