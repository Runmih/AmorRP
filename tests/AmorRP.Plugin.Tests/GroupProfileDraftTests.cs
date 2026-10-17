using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Features.Communities;
using Xunit;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Tests;

public sealed class GroupProfileDraftTests
{
    private static GroupProfile Profile(string joinMode = "open") => new(
        Guid.NewGuid(), Guid.NewGuid(), "Patrol", "Evening patrol", null, 1, 1,
        "\"group:original:1\"", Guid.NewGuid(), joinMode, true);

    [Theory]
    [InlineData("name", false)]
    [InlineData("name", true)]
    [InlineData("description", false)]
    [InlineData("description", true)]
    [InlineData("joining", false)]
    [InlineData("joining", true)]
    [InlineData("password", false)]
    [InlineData("password", true)]
    public void Refresh_preserves_each_unfinished_field_and_the_original_save_precondition(string field, bool remoteEdit)
    {
        var original = Profile(field == "password" ? "password" : "open");
        var draft = new GroupProfileDraft();
        draft.Load(original);

        switch (field)
        {
            case "name": draft.Name = "Night patrol"; break;
            case "description": draft.Description = "Meet at the gate"; break;
            case "joining": draft.JoinMode = 1; break;
            case "password": draft.Password = "new-test-password"; break;
        }
        var unfinished = draft.Request();
        var refreshed = remoteEdit
            ? original with { Name = "Remote edit", Description = "Changed elsewhere", JoinMode = "closed", Version = 2, Etag = "\"group:original:2\"" }
            : original with { IsFavorite = true };

        draft.Refresh(refreshed);
        draft.Refresh(refreshed);

        Assert.Equal(unfinished, draft.Request());
        Assert.Equal(original.Etag, draft.ETag);
        Assert.True(draft.IsDirty);
    }

    [Fact]
    public void An_unedited_draft_accepts_fresh_profile_settings_and_version()
    {
        var original = Profile();
        var draft = new GroupProfileDraft();
        draft.Load(original);
        var refreshed = original with { Name = "Updated patrol", Description = "New description", JoinMode = "closed", Version = 2, Etag = "\"group:original:2\"" };

        draft.Refresh(refreshed);

        Assert.Equal(new GroupWrite(refreshed.Name, refreshed.Description, "closed"), draft.Request());
        Assert.Equal(refreshed.Etag, draft.ETag);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Reverting_all_edits_allows_background_updates_again()
    {
        var original = Profile();
        var draft = new GroupProfileDraft();
        draft.Load(original);
        draft.Name = "Temporary name";
        draft.Description = "Temporary description";
        draft.JoinMode = 2;
        draft.Password = "temporary-test-password";
        Assert.True(draft.IsDirty);

        draft.Name = original.Name;
        draft.Description = original.Description;
        draft.JoinMode = 0;
        draft.Password = "";
        Assert.False(draft.IsDirty);
        draft.Refresh(original with { JoinMode = "closed" });

        Assert.Equal("closed", draft.Request().JoinMode);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Explicit_reload_or_successful_save_adopts_the_profile_and_clears_the_password()
    {
        var original = Profile("password");
        var draft = new GroupProfileDraft();
        draft.Load(original);
        draft.Name = "Unfinished name";
        draft.Password = "new-test-password";
        var saved = original with { Name = "Saved name", Version = 2, Etag = "\"group:original:2\"" };

        draft.Load(saved);

        Assert.Equal(new GroupWrite(saved.Name, saved.Description, "password"), draft.Request());
        Assert.Equal(saved.Etag, draft.ETag);
        Assert.Equal("", draft.Password);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Selecting_a_different_group_discards_the_previous_groups_password_and_edits()
    {
        var draft = new GroupProfileDraft();
        draft.Load(Profile("password"));
        draft.Password = "previous-group-test-password";
        draft.Description = "Unfinished description";
        var other = Profile("closed");

        draft.Load(other);

        Assert.Equal(new GroupWrite(other.Name, other.Description, "closed"), draft.Request());
        Assert.Equal(other.Etag, draft.ETag);
        Assert.Equal("", draft.Password);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void Clearing_the_context_removes_the_password_and_returns_to_new_group_defaults()
    {
        var draft = new GroupProfileDraft();
        draft.Load(Profile("password"));
        draft.Password = "previous-character-test-password";

        draft.Clear();

        Assert.Equal(new GroupWrite("", "", "open"), draft.Request());
        Assert.Null(draft.ETag);
        Assert.Equal("", draft.Password);
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void A_logo_save_updates_the_version_without_clearing_unfinished_profile_settings()
    {
        var original = Profile();
        var draft = new GroupProfileDraft();
        draft.Load(original);
        draft.JoinMode = 2;
        draft.Password = "new-test-password";
        var unfinished = draft.Request();

        draft.AcceptLogoVersion("\"group:original:2\"");
        draft.Refresh(original with { Version = 2, Etag = "\"group:original:2\"", LogoAssetId = Guid.NewGuid() });

        Assert.Equal(unfinished, draft.Request());
        Assert.Equal("\"group:original:2\"", draft.ETag);
        Assert.True(draft.IsDirty);
    }
}
