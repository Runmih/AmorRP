using System.Numerics;
using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Media;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private GroupProfile[] communityGroups = [];
    private Guid? selectedGroupId;
    private bool creatingGroup;
    private readonly GroupProfileDraft groupDraft = new();
    private string requestedGroupLogo = "";
    private readonly CommunityImageCache groupLogos = new(textures, "group logo");
    private bool CanCreateGroups => Owner || community?.MyCapabilities.Contains("groups.create") == true;
    private bool OwnSelectedGroup => SelectedGroup?.OwnerCharacterId == session?.Character.Id;
    private bool CanManageGroups => Owner || community?.MyCapabilities.Contains("groups.manage") == true;
    private GroupProfile? SelectedGroup => communityGroups.FirstOrDefault(x => x.Id == selectedGroupId);
    private async Task<GroupProfile[]> LoadCommunityGroupsAsync(Uri uri, string token, Guid communityId, CancellationToken ct)
    {
        var result = new List<GroupProfile>(); string? cursor = null;
        do {
            var page = await client.GetAsync<GroupPage>(uri, $"api/v2/communities/{communityId}/groups" + (cursor == null ? "" : "?cursor=" + Uri.EscapeDataString(cursor)), token, ct);
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
        groupPrivate = null; privateRequestedFor = null; selectedGroupId = row.Id; creatingGroup = false; noticeDraftFor = null; NewGroupEvent(); groupAnnouncementTitle = groupAnnouncementMessage = ""; groupDraft.Load(row);
        groupLogos.Clear(); requestedGroupLogo = "";
    }
    private void ClearNestedGroups()
    {
        groupPrivate = null; privateRequestedFor = null; hub = new([], []); favoriteEvents = new([]); groupJoinPassword = ""; noticeDraftFor = null;
        communityGroups = []; selectedGroupId = null; creatingGroup = false;
        groupDraft.Clear(); requestedGroupLogo = ""; groupLogos.Clear();
    }
    private void DrawCommunityGroups()
    {
        if (community == null) { Wrapped("Choose a community first to see its groups."); return; }
        groupLogos.Draw();
        Wrapped("Groups within " + community.Name);
        if (CanCreateGroups && ImGui.Button("New group"))
        {
            groupDraft.Clear();
            creatingGroup = true; selectedGroupId = null;
            groupLogos.Clear(); requestedGroupLogo = "";
        }
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 38;
        if (!ImGui.BeginTable("CommunityGroupsLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Groups", ImGuiTableColumnFlags.WidthStretch, .32f);
        if (!narrow) ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthStretch, .68f);
        ImGui.TableNextColumn(); ImGui.BeginChild("CommunityGroupList", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 7 : 0), true);
        foreach (var row in communityGroups.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            if (ImGui.Selectable(row.Name + "###nested" + row.Id, selectedGroupId == row.Id)) SelectGroup(row);
        if (communityGroups.Length == 0) Wrapped("No groups yet.");
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("CommunityGroupProfile", Vector2.Zero, true); ImGui.PushItemWidth(-1);
        var current = SelectedGroup;
        if (creatingGroup || current != null)
        {
            if (current != null) DrawNestedGroupLogo(current);
            if (creatingGroup ? CanCreateGroups : OwnSelectedGroup)
            {
                FieldLabel("Group name"); LimitedInput("##NestedGroupName", ref groupDraft.Name, 80, countCodeUnits: true);
                FieldLabel("Description"); WrappedInput("##NestedGroupDescription", ref groupDraft.Description, 8192, new(-1, ImGui.GetTextLineHeightWithSpacing() * 5), maximumCharacters: 300);
                ImGui.TextDisabled($"{PlainText.Characters(groupDraft.Description)}/300");
                FieldLabel("Joining"); ImGui.Combo("##GroupJoinMode", ref groupDraft.JoinMode, new[] { "Open", "Closed", "Password protected" }, 3);
                if (groupDraft.JoinMode == 2) { FieldLabel(current?.JoinMode == "password" ? "New password (leave blank to keep current)" : "Password"); ImGui.InputText("##GroupPassword", ref groupDraft.Password, 65, ImGuiInputTextFlags.Password); }
                var error = PlainText.Error(groupDraft.Name, 80, required: true) ?? PlainText.Error(groupDraft.Description, 300, multiline: true, required: false);
                var conflict = current != null && groupDraft.ETag != current.Etag;
                if (error != null) Wrapped(error);
                if (conflict)
                {
                    Wrapped("This group changed elsewhere. Reload its profile before saving.");
                    if (ImGui.Button("Reload group")) groupDraft.Load(current!);
                }
                var dirty = creatingGroup || groupDraft.IsDirty;
                ImGui.BeginDisabled(error != null || conflict || !dirty);
                if (ActionButton(creatingGroup ? "Create group" : "Save group"))
                    Mutate(creatingGroup ? "POST" : "PATCH", $"api/v2/communities/{community.Id}/groups" + (creatingGroup ? "" : "/" + current!.Id), groupDraft.Request(), groupDraft.ETag);
                ImGui.EndDisabled();
                if (creatingGroup) { if (ImGui.Button("Cancel")) { creatingGroup = false; if (communityGroups.FirstOrDefault() is { } first) SelectGroup(first); } }

            }
            else
            {
                Wrapped(current!.Name); ImGui.Separator();
                Wrapped(current.Description.Length == 0 ? "No group description yet." : current.Description);
            }
        }
        if (current != null)
        {
            ImGui.Separator();
            if (ActionButton(current.IsFavorite ? "Unfavorite group" : "Favorite group")) Mutate(current.IsFavorite ? "DELETE" : "PUT", GroupPath(current) + "/favorite", null);
            DrawGroupSections(current);
            if (OwnSelectedGroup || CanManageGroups)
            {
                ImGui.Separator();
                if (ShiftMemberButton("Delete group")) Mutate("DELETE", GroupPath(current), null, current.Etag);
            }
        }
        else if (!creatingGroup) Wrapped(CanCreateGroups ? "Select a group, or choose New group to create one." : "Select a group to read its profile.");
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
    private void DrawNestedGroupLogo(GroupProfile current)
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
                    var bytes = await client.GetImageAsync(uri, $"api/v2/communities/{communityId}/groups/{groupId}/logo", token, 1048576, ct);
                    return () => { if (community?.Id == communityId && selectedGroupId == groupId && requestedGroupLogo == imageKey) groupLogos.Update(imageKey, bytes, true); };
                }, "Loading group logo…");
            }
        }
        var size = new Vector2(ImGui.GetFontSize() * 7); var position = ImGui.GetCursorScreenPos();
        ImGui.BeginDisabled(!OwnSelectedGroup || WriteBlocked);
        var clicked = groupLogos.HasImage ? ImGui.InvisibleButton("##NestedGroupLogo", size) : ImGui.Button(OwnSelectedGroup ? "Add logo" : "Group logo", size);
        if (groupLogos.HasImage) groupLogos.DrawInBox(position, size);
        ImGui.EndDisabled();
        if (OwnSelectedGroup && ImGui.BeginPopupContextItem("NestedGroupLogoActions"))
        {
            ImGui.BeginDisabled(WriteBlocked || current.LogoAssetId == null);
            if (ImGui.MenuItem("Remove logo")) Mutate("DELETE", $"api/v2/communities/{communityId}/groups/{current.Id}/logo", null, ETag("group-logo", current.Id, current.LogoVersion));
            ImGui.EndDisabled(); ImGui.EndPopup();
        }
        ImGui.Spacing();
        if (!clicked) return;
        var actor = session.Character.Id; var selected = current.Id; var etag = ETag("group-logo", current.Id, current.LogoVersion);
        files.OpenFileDialog("Group logo", ".png,.jpg,.jpeg,.webp", (ok, path) => {
            if (!ok || community?.Id != communityId || session?.Character.Id != actor || selectedGroupId != selected || !OwnSelectedGroup || WriteBlocked) return;
            Start(async ct => {
                await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
                var maximum = serviceLimits?.MaxCommunityLogoBytes ?? 2097152;
                if (file.Length <= 0 || file.Length > maximum) throw new InvalidOperationException("Choose a PNG, JPG or WebP smaller than 2 MiB.");
                var bytes = new byte[(int)file.Length]; await file.ReadExactlyAsync(bytes, ct);
                return () => { if (community?.Id == communityId && selectedGroupId == selected && OwnSelectedGroup) QueueUpload($"api/v2/communities/{communityId}/groups/{selected}/logo", etag, bytes); };
            }, "Reading group logo…");
        });
    }
    private void ApplyNestedGroupSuccess(CommandRequest request, string json)
    {
        if (!request.Path.Contains("/groups", StringComparison.Ordinal)) return;
        var tail = request.Path.Split('/');
        if (tail.Length > 6 && !request.Path.EndsWith("/logo", StringComparison.Ordinal))
        { groupPrivate = null; privateRequestedFor = null; groupJoinPassword = "";
            if (request.Path.EndsWith("/notice", StringComparison.Ordinal)) noticeDraftFor = null;
            if (request.Path.Contains("/events", StringComparison.Ordinal)) NewGroupEvent();
            if (request.Path.EndsWith("/announcements", StringComparison.Ordinal)) groupAnnouncementTitle = groupAnnouncementMessage = "";
            refreshNeeded = true; return; }
        var row = JsonSerializer.Deserialize<CommandResult<GroupProfile>>(json, FoundationClient.Json)!.Result;
        var deleted = request.Method == "DELETE" && !request.Path.EndsWith("/logo", StringComparison.Ordinal);
        communityGroups = communityGroups.Where(x => x.Id != row.Id).Concat(deleted ? [] : new[] { row }).ToArray();
        if (deleted) { selectedGroupId = null; if (communityGroups.FirstOrDefault() is { } next) SelectGroup(next); }
        else { groupPrivate = null; privateRequestedFor = null; selectedGroupId = row.Id; creatingGroup = false; if (request.Path.EndsWith("/logo", StringComparison.Ordinal)) groupDraft.AcceptLogoVersion(row.Etag); else groupDraft.Load(row); }
    }
}
