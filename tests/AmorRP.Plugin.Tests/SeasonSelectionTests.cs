using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class SeasonSelectionTests
{
    private static Community Season(string state) => new(Guid.NewGuid(), "Season", "", Guid.NewGuid(), 1,
        new Currency(Guid.NewGuid(), Guid.NewGuid(), "Coins", "C", 1), [], false,
        new Balance(Guid.NewGuid(), Guid.NewGuid(), "0", "0", "0", 1), null, SeasonState: state);

    [Fact]
    public void Login_and_rollover_choose_active_while_archive_browsing_survives_refresh()
    {
        var archive = Season("archived"); var active = Season("active"); var next = Season("active");
        Community[] list = [archive, active];
        Assert.Equal(active, SeasonSelection.Choose(list, archive.Id, active.Id, null, false));
        Assert.Equal(archive, SeasonSelection.Choose(list, archive.Id, active.Id, active.Id, true));
        Assert.Equal(next, SeasonSelection.Choose([archive, active, next], active.Id, next.Id, active.Id, true));
        Assert.Equal(active, SeasonSelection.Choose(list, Guid.NewGuid(), active.Id, active.Id, true));
    }

    [Theory]
    [InlineData(true, "active", false)]
    [InlineData(true, "draft", true)]
    [InlineData(true, "legacy", true)]
    [InlineData(true, "archived", true)]
    [InlineData(false, "archived", true)]
    [InlineData(false, "legacy", false)]
    public void Archive_remains_read_only_even_if_rollout_flag_is_disabled(bool enabled, string state, bool readOnly)
        => Assert.Equal(readOnly, SeasonSelection.ReadOnly(enabled, Season(state)));

    [Fact]
    public void Rollover_between_discovery_and_membership_reads_is_observed_on_the_next_refresh()
    {
        var old = Season("active"); var next = Season("active");
        var observed = SeasonSelection.ObservedActive([old], next.Id);
        Assert.Equal(old.Id, observed);
        var latest = new[] { old with { SeasonState = "archived" }, next };
        Assert.Equal(next.Id, SeasonSelection.ObservedActive(latest, old.Id));
        Assert.Equal(next, SeasonSelection.Choose(latest, old.Id,
            SeasonSelection.ObservedActive(latest, next.Id), observed, true));
    }
}
