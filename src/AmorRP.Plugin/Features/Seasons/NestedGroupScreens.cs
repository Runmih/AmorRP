using System.Numerics;
using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Media;
using AmorRP.Plugin.Features.Shell;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private enum GroupView { Browse, Management, CommunityAdministration }
    private Guid? pendingGroupSelection, requestedDirectoryGroup;
    private bool pendingGroupCreation;
    private GroupProfile[] communityGroups = [];
    private Guid? selectedGroupId;
    private bool creatingGroup;
    private readonly GroupProfileDraft groupDraft = new();
    private string requestedGroupLogo = "";
    private readonly SeasonImageCache groupLogos = new(textures, "group logo");
    private bool CanCreateGroups => Owner || community?.MyCapabilities.Contains("groups.create") == true;
    private bool OwnSelectedGroup => SelectedGroup?.OwnerCharacterId == session?.Character.Id;
    private bool CanManageGroups => Owner || community?.MyCapabilities.Contains("groups.manage") == true;
    private GroupProfile? SelectedGroup => communityGroups.FirstOrDefault(x => x.Id == selectedGroupId);
    private async Task<GroupProfile[]> LoadCommunityGroupsAsync(Uri uri, string token, Guid communityId, CancellationToken ct)
    {
        var result = new List<GroupProfile>(); string? cursor = null;
        do {
            var page = await client.GetAsync<GroupPage>(uri, $"api/v2/seasons/{communityId}/groups" + (cursor == null ? "" : "?cursor=" + Uri.EscapeDataString(cursor)), token, ct);
            result.AddRange(page.Items); cursor = page.NextCursor;
        } while (cursor != null);
        return result.ToArray();
    }
    private void AcceptCommunityGroups(GroupProfile[] rows)
    {
        communityGroups = rows;
        if (!creatingGroup && SelectedGroup == null && rows.FirstOrDefault() is { } first) SelectGroup(first);
        else if (!creatingGroup && SelectedGroup is { } current)
            groupDraft.Refresh(current);
    }
    private void SelectGroup(GroupProfile row)
    {
        ClearGroupPermissions(); ClearTreasury(); ClearGroupBilling();
        groupPrivate = null; privateRequestedFor = null; selectedGroupId = row.Id; creatingGroup = false; noticeDraftFor = null; NewGroupEvent(); groupAnnouncementTitle = groupAnnouncementMessage = ""; groupDraft.Load(row);
        groupLogos.Clear(); requestedGroupLogo = "";
    }
    private void ClearNestedGroups()
    {
        pendingGroupSelection = requestedDirectoryGroup = null; pendingGroupCreation = false;
        ClearTreasuryReturns();
        ClearGroupPermissions(); ClearTreasury(); ClearGroupBilling();
        groupPrivate = null; privateRequestedFor = null; hub = new([], []); groupJoinPassword = ""; noticeDraftFor = null;
        communityGroups = []; selectedGroupId = null; creatingGroup = false;
        groupDraft.Clear(); requestedGroupLogo = ""; groupLogos.Clear();
    }
    private void DrawCommunityGroups(GroupView view)
    {
        var management = view == GroupView.Management;
        var administration = view == GroupView.CommunityAdministration;
        if (!administration) DrawTreasuryReturns();
        if (community == null) { Wrapped("Choose a season first to see its groups."); return; }
        DrawRefreshState(groupsRefresh, "Groups");
        groupLogos.Draw();
        if (ActionButton("Refresh groups", mutation: false)) { groupsRefresh.Invalidate(); hubRefresh.Invalidate(); ClearTreasury(); if (SelectedGroup is { IsMember: true } selectedGroup) LoadGroupPrivate(selectedGroup, true); }
        var canCreateHere = management && CanCreateGroups;
        var createRequested = canCreateHere && ImGui.Button("New group");
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 38;
        if (!ImGui.BeginTable("CommunityGroupsLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Groups", ImGuiTableColumnFlags.WidthStretch, .32f);
        if (!narrow) ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthStretch, .68f);
        ImGui.TableNextColumn(); ImGui.BeginChild("CommunityGroupList", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 7 : 0), true);
        if (createRequested) RequestGroupChange(null);
        if (requestedDirectoryGroup is { } requested && view == GroupView.Browse)
        { requestedDirectoryGroup = null; if (communityGroups.FirstOrDefault(x => x.Id == requested) is { } requestedGroup) RequestGroupChange(requestedGroup); }
        var shownGroups = communityGroups.Where(x => !management || WorkspaceAccess.GroupManagement(community, session?.Character.Id, x)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var row in shownGroups)
            if (ImGui.Selectable(row.Name + "###nested" + row.Id, selectedGroupId == row.Id)) RequestGroupChange(row);
        if (shownGroups.Length == 0) Wrapped(management ? "No groups you can manage yet." : "No groups yet.");
        DrawGroupSwitchDialog();
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("CommunityGroupProfile", Vector2.Zero, true); ImGui.PushItemWidth(-1);
        var current = SelectedGroup;
        if (management && current != null && !WorkspaceAccess.GroupManagement(community, session?.Character.Id, current)) current = null;
        if (creatingGroup && canCreateHere)
            DrawGroupSections(null, view);
        else if (current == null)
            Wrapped(canCreateHere ? "Select a group, or choose New group to create one." : "Select a group to read its profile.");
        else if (administration)
        {
            DrawGroupProfile(current, editable: false);
            ImGui.Separator();
            if (Owner) DrawGroupFeatures(current);
            if (CanManageGroups) DrawGroupDelete(current);
        }
        else DrawGroupSections(current, view);
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }

    private void DrawGroupMain(GroupProfile? group, GroupView view)
    {
        var management = view == GroupView.Management;
        DrawGroupProfile(group, editable: management && (group == null || OwnSelectedGroup));
        if (group == null) return;

        ImGui.Separator();
        if (view == GroupView.Browse)
        {
            if (ActionButton(group.IsFavorite ? "Unfavorite group" : "Favorite group"))
                Mutate(group.IsFavorite ? "DELETE" : "PUT", GroupPath(group) + "/favorite", null);
            if (WorkspaceAccess.GroupManagement(community, session?.Character.Id, group)
                && ImGui.Button("Manage this group")) OpenWorkspace(WorkspaceKind.GroupManagement);
        }
        DrawTrust(group);
        if (management && OwnSelectedGroup) DrawGroupDelete(group);
    }

    private void DrawGroupProfile(GroupProfile? group, bool editable)
    {
        if (group != null) DrawNestedGroupLogo(group, editable);
        if (!editable)
        {
            if (group != null)
            {
                Wrapped(group.Name);
                Wrapped(group.Description.Length == 0 ? "No group description yet." : group.Description);
            }
            return;
        }

        FieldLabel("Group name"); LimitedInput("##NestedGroupName", ref groupDraft.Name, 80, countCodeUnits: true);
        FieldLabel("Description"); WrappedInput("##NestedGroupDescription", ref groupDraft.Description, 8192, new(-1, ImGui.GetTextLineHeightWithSpacing() * 5), maximumCharacters: 300);
        ImGui.TextDisabled($"{PlainText.Characters(groupDraft.Description)}/300");
        FieldLabel("Joining"); ImGui.Combo("##GroupJoinMode", ref groupDraft.JoinMode, ["Open", "Closed", "Password protected"], 3);
        if (groupDraft.JoinMode == 2)
        {
            FieldLabel(group?.JoinMode == "password" ? "New password (leave blank to keep current)" : "Password");
            ImGui.InputText("##GroupPassword", ref groupDraft.Password, 65, ImGuiInputTextFlags.Password);
        }
        var error = PlainText.Error(groupDraft.Name, 80, required: true) ?? PlainText.Error(groupDraft.Description, 300, multiline: true, required: false);
        var conflict = group != null && groupDraft.ETag != group.Etag;
        if (error != null) Wrapped(error);
        if (conflict)
        {
            Wrapped("This group changed elsewhere. Reload its profile before saving.");
            if (ImGui.Button("Reload group")) groupDraft.Load(group!);
        }
        ImGui.BeginDisabled(error != null || conflict || group != null && !groupDraft.IsDirty);
        if (ActionButton(group == null ? "Create group" : "Save group"))
        {
            var path = group == null ? $"api/v2/seasons/{community!.Id}/groups" : GroupPath(group);
            Mutate(group == null ? "POST" : "PATCH", path, groupDraft.Request(), groupDraft.ETag);
        }
        ImGui.EndDisabled();
        if (group == null && ImGui.Button("Cancel"))
        {
            creatingGroup = false;
            if (communityGroups.FirstOrDefault() is { } first) SelectGroup(first);
        }
    }

    private void DrawGroupDelete(GroupProfile group)
    {
        ImGui.Separator();
        if (ShiftMemberButton("Delete group")) Mutate("DELETE", GroupPath(group), null, group.Etag);
    }

    private void DrawNestedGroupLogo(GroupProfile current, bool editable)
    {
        var communityId = community!.Id; var imageKey = $"{origin}|{session!.Character.Id}|{communityId}|{current.Id}|{current.LogoAssetId}|{current.LogoVersion}";
        if (requestedGroupLogo != imageKey && connectionError.Length == 0)
        {
            requestedGroupLogo = imageKey;
            if (current.LogoAssetId == null) groupLogos.Update(imageKey, null, false);
            else
            {
                var uri = origin!; var token = session.AccessToken; var groupId = current.Id;
                Start(async ct => {
                    var bytes = await client.GetImageAsync(uri, $"api/v2/seasons/{communityId}/groups/{groupId}/logo", token, 1048576, ct);
                    return () => { if (community?.Id == communityId && selectedGroupId == groupId && requestedGroupLogo == imageKey) groupLogos.Update(imageKey, bytes, true); };
                }, "Loading group logo…");
            }
        }
        var size = new Vector2(ImGui.GetFontSize() * 7); var position = ImGui.GetCursorScreenPos();
        ImGui.BeginDisabled(!editable || !OwnSelectedGroup || WriteBlocked);
        var clicked = groupLogos.HasImage ? ImGui.InvisibleButton("##NestedGroupLogo", size) : ImGui.Button(editable && OwnSelectedGroup ? "Add logo" : "Group logo", size);
        if (groupLogos.HasImage) groupLogos.DrawInBox(position, size);
        ImGui.EndDisabled();
        if (editable && OwnSelectedGroup && ImGui.BeginPopupContextItem("NestedGroupLogoActions"))
        {
            ImGui.BeginDisabled(WriteBlocked || current.LogoAssetId == null);
            if (ImGui.MenuItem("Remove logo")) Mutate("DELETE", $"api/v2/seasons/{communityId}/groups/{current.Id}/logo", null, ETag("group-logo", current.Id, current.LogoVersion));
            ImGui.EndDisabled(); ImGui.EndPopup();
        }
        ImGui.Spacing();
        if (!editable || !clicked) return;
        var actor = session.Character.Id; var selected = current.Id; var etag = ETag("group-logo", current.Id, current.LogoVersion);
        files.OpenFileDialog("Group logo", ".png,.jpg,.jpeg,.webp", (ok, path) => {
            if (!ok || community?.Id != communityId || session?.Character.Id != actor || selectedGroupId != selected || !OwnSelectedGroup || WriteBlocked) return;
            Start(async ct => {
                await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
                var maximum = serviceLimits?.MaxCommunityLogoBytes ?? 2097152;
                if (file.Length <= 0 || file.Length > maximum) throw new InvalidOperationException("Choose a PNG, JPG or WebP smaller than 2 MiB.");
                var bytes = new byte[(int)file.Length]; await file.ReadExactlyAsync(bytes, ct);
                return () => { if (community?.Id == communityId && selectedGroupId == selected && OwnSelectedGroup) QueueUpload($"api/v2/seasons/{communityId}/groups/{selected}/logo", etag, bytes); };
            }, "Reading group logo…");
        });
    }
    private bool HasGroupDrafts()
    {
        var notice = hub.Notices.FirstOrDefault(x => x.GroupId == selectedGroupId);
        return ((creatingGroup || OwnSelectedGroup) && groupDraft.IsDirty) || groupStaffDraft.IsDirty || groupFeaturesDraft.IsDirty
            || groupAnnouncementTitle.Length > 0 || groupAnnouncementMessage.Length > 0 || billingAmount.Length > 0 || billingService.Length > 0
            || treasuryAmount.Length > 0 || treasuryNote.Length > 0 || eventEditId.HasValue || eventTitle.Length > 0 || eventDescription.Length > 0 || eventLocation.Length > 0
            || (noticeDraftFor.HasValue && (noticeTitle != (notice?.Title ?? "") || noticeMessage != (notice?.Message ?? "") || noticeReward != (notice?.Reward ?? "")));
    }
    private void RequestGroupChange(GroupProfile? group)
    {
        if (group != null && group.Id == selectedGroupId && !creatingGroup) return;
        if (HasGroupDrafts())
        {
            pendingGroupSelection = group?.Id; pendingGroupCreation = group == null;
            ImGui.OpenPopup("Switch group and discard drafts?");
        }
        else ApplyGroupChange(group);
    }
    private void ApplyGroupChange(GroupProfile? group)
    {
        if (group != null) { SelectGroup(group); return; }
        ClearGroupPermissions(); ClearTreasury(); ClearGroupBilling(); groupDraft.Clear();
        groupPrivate = null; privateRequestedFor = null; creatingGroup = true; selectedGroupId = null;
        groupLogos.Clear(); requestedGroupLogo = ""; noticeDraftFor = null; NewGroupEvent();
        groupAnnouncementTitle = groupAnnouncementMessage = "";
    }
    private void DrawGroupSwitchDialog()
    {
        if (!ImGui.BeginPopupModal("Switch group and discard drafts?", ImGuiWindowFlags.AlwaysAutoResize)) return;
        Wrapped("You have unfinished group changes. Switching groups will discard those drafts.");
        if (ImGui.Button("Discard drafts and switch"))
        {
            var target = communityGroups.FirstOrDefault(x => x.Id == pendingGroupSelection);
            if (pendingGroupCreation || target != null) ApplyGroupChange(target);
            pendingGroupSelection = null; pendingGroupCreation = false; ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine(); if (ImGui.Button("Keep editing"))
        { pendingGroupSelection = null; pendingGroupCreation = false; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }
    private void ApplyGroupActivitySuccess(CommandRequest request)
    {
        if (request.Operation.Kind == CommandKind.EditGroupStaff) groupStaffDraft.Clear();
        groupPrivate = null; privateRequestedFor = null; groupJoinPassword = "";
        switch (request.Operation.Kind)
        {
            case CommandKind.SaveGroupNotice:
            case CommandKind.DeleteGroupNotice: noticeDraftFor = null; break;
            case CommandKind.CreateGroupEvent:
            case CommandKind.EditGroupEvent:
            case CommandKind.DeleteGroupEvent: NewGroupEvent(); break;
            case CommandKind.PublishGroupAnnouncement: groupAnnouncementTitle = groupAnnouncementMessage = ""; break;
        }
    }
    private void ApplyGroupProfileSuccess(CommandRequest request, string json)
    {
        var row = ReadCommand<GroupProfile>(json);
        if (request.Operation.Kind is CommandKind.EditGroupFeatures or CommandKind.EditGroupTrust)
        {
            communityGroups = communityGroups.Select(x => x.Id == row.Id ? row : x).ToArray();
            if (request.Operation.Kind == CommandKind.EditGroupFeatures) groupFeaturesDraft.Load(row.Id.ToString(), row.Etag, row.EnabledFeatures ?? []);
            else groupFeaturesDraft.Refresh(row.Id.ToString(), row.Etag, row.EnabledFeatures ?? []);
            groupPrivate = null; privateRequestedFor = null; return;
        }
        var deleted = request.Operation.Kind == CommandKind.DeleteGroup;
        if (deleted) ClearTreasuryReturns();
        communityGroups = communityGroups.Where(x => x.Id != row.Id).Concat(deleted ? [] : new[] { row }).ToArray();
        if (deleted) { selectedGroupId = null; if (communityGroups.FirstOrDefault() is { } next) SelectGroup(next); }
        else
        {
            groupPrivate = null; privateRequestedFor = null; selectedGroupId = row.Id; creatingGroup = false;
            if (request.Operation.Kind is CommandKind.SetGroupLogo or CommandKind.RemoveGroupLogo) groupDraft.AcceptLogoVersion(row.Etag);
            else groupDraft.Load(row);
        }
    }
}
