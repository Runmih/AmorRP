using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class SeasonSelectionTests
{
    [Theory]
    [InlineData("draft")]
    [InlineData("archived")]
    public void Inactive_seasons_allow_lifecycle_and_global_permission_commands_but_not_gameplay(string state)
    {
        var season = Season(state);
        Assert.True(SeasonSelection.CanSubmit(season, new("POST", $"api/v2/seasons/{season.Id}/activation", "{}", null)));
        Assert.True(SeasonSelection.CanSubmit(season, new("POST", $"api/v2/seasons/{season.Id}/archive", "{}", null)));
        Assert.True(SeasonSelection.CanSubmit(season, new("PUT", $"api/v2/seasons/creation-permissions/{Guid.NewGuid()}", "{}", null)));
        Assert.False(SeasonSelection.CanSubmit(season, new("POST", $"api/v2/seasons/{season.Id}/inventory/potions", "{}", null)));
        Assert.False(SeasonSelection.CanSubmit(season, new("PATCH", $"api/v2/seasons/{season.Id}", "{}", null)));
    }

    [Fact]
    public void Creation_controls_require_the_explicit_global_permission()
    {
        Assert.False(SeasonSelection.CanCreate(null));
        Assert.False(SeasonSelection.CanCreate(new(false, null, [], [])));
        Assert.False(SeasonSelection.CanCreate(new(true, null, [], [])));
        Assert.False(SeasonSelection.CanCreate(new(false, null, [], ["groups.create", "season.moderate"])));
        Assert.True(SeasonSelection.CanCreate(new(false, null, [], [SeasonCapabilities.Create])));
        Assert.True(SeasonSelection.CanCreate(new(true, null, [], [SeasonCapabilities.Create])));
    }
    private static Season Season(string state) => new(Guid.NewGuid(), "Season", "", Guid.NewGuid(), 1,
        new Currency(Guid.NewGuid(), Guid.NewGuid(), "Coins", "C", 1), [], false,
        new Balance(Guid.NewGuid(), Guid.NewGuid(), "0", "0", "0", 1), null, SeasonState: state);

    [Fact]
    public void Login_and_rollover_choose_active_while_archive_browsing_survives_refresh()
    {
        var archive = Season("archived"); var active = Season("active"); var next = Season("active");
        Season[] list = [archive, active];
        Assert.Equal(active, SeasonSelection.Choose(list, archive.Id, active.Id, null, false, canBrowse: true));
        Assert.Equal(archive, SeasonSelection.Choose(list, archive.Id, active.Id, active.Id, true, canBrowse: true));
        Assert.Equal(next, SeasonSelection.Choose([archive, active, next], active.Id, next.Id, active.Id, true, canBrowse: true));
        Assert.Equal(active, SeasonSelection.Choose(list, Guid.NewGuid(), active.Id, active.Id, true, canBrowse: true));
    }

    [Fact]
    public void Players_continue_to_follow_the_active_season_with_an_old_browsing_selection()
    {
        var archive = Season("archived");
        var active = Season("active");
        Assert.Equal(active, SeasonSelection.Choose([archive, active], archive.Id, active.Id, active.Id, true, canBrowse: false));
        Assert.Null(SeasonSelection.Choose([archive], archive.Id, null, active.Id, true, canBrowse: false));
        Assert.Null(SeasonSelection.Choose([archive], archive.Id, archive.Id, active.Id, true, canBrowse: false));
    }

    [Theory]
    [InlineData("active", false)]
    [InlineData("draft", true)]
    [InlineData("archived", true)]
    public void Only_the_active_season_is_writable(string state, bool readOnly)
        => Assert.Equal(readOnly, SeasonSelection.ReadOnly(Season(state)));

    [Fact]
    public void Rollover_between_discovery_and_membership_reads_is_observed_on_the_next_refresh()
    {
        var old = Season("active"); var next = Season("active");
        var observed = SeasonSelection.ObservedActive([old], next.Id);
        Assert.Equal(old.Id, observed);
        var latest = new[] { old with { SeasonState = "archived" }, next };
        Assert.Equal(next.Id, SeasonSelection.ObservedActive(latest, old.Id));
        Assert.Equal(next, SeasonSelection.Choose(latest, old.Id,
            SeasonSelection.ObservedActive(latest, next.Id), observed, true, canBrowse: false));
    }
}
