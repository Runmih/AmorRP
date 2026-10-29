using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Features.Communities;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class GroupPermissionDraftTests
{
    [Fact]
    public void Refresh_preserves_unsaved_grants_and_their_original_precondition()
    {
        var draft = new GroupPermissionDraft();
        draft.Refresh("group:member", "v1", []);
        draft.Set(GroupCapabilities.Events, true);
        draft.Refresh("group:member", "v2", [GroupCapabilities.Notice]);
        Assert.True(draft.IsDirty); Assert.Equal("v1", draft.ETag);
        Assert.Equal(new[] { GroupCapabilities.Events }, draft.Actions);
        draft.Load("group:member", "v2", [GroupCapabilities.Notice]);
        Assert.False(draft.IsDirty); Assert.Equal("v2", draft.ETag);
    }

    [Fact]
    public void Switching_group_or_member_cannot_carry_a_grant_draft_to_the_new_target()
    {
        var draft = new GroupPermissionDraft(); draft.Load("first:member", "v1", []);
        draft.Set(GroupCapabilities.Announcements, true);
        draft.Refresh("second:member", "v1", []);
        Assert.Empty(draft.Actions); Assert.False(draft.IsDirty);
        draft.Set(GroupCapabilities.Events, true); draft.Clear();
        Assert.Empty(draft.Actions); Assert.Null(draft.ETag);
    }

    [Fact]
    public void Clean_drafts_follow_server_changes_and_group_replies_cannot_target_another_editor()
    {
        var draft = new GroupPermissionDraft(); draft.Load("group:member", "v1", [GroupCapabilities.Notice]);
        draft.Refresh("group:member", "v2", []);
        Assert.Empty(draft.Actions); Assert.Equal("v2", draft.ETag);
        var group = Guid.NewGuid();
        var request = new CommandRequest("PUT", $"api/v2/communities/{Guid.NewGuid()}/groups/{group}/features", "{}", "v1");
        Assert.True(request.AppliesToGroup(group)); Assert.False(request.AppliesToGroup(Guid.NewGuid()));
        Assert.False(request.AppliesToGroup(null));
        Assert.Equal("Group features saved.", UserFeedback.Success(request));
    }
}
