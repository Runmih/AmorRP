using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private readonly GroupPermissionDraft groupFeaturesDraft = new(), groupStaffDraft = new();
    private Guid? groupStaffSelection;
    private bool CanGroupAction(GroupProfile group, string action) => group.IsMember
        && group.EnabledFeatures?.Contains(action) == true
        && (group.OwnerCharacterId == session?.Character.Id || group.MyCapabilities?.Contains(action) == true);

    private static string GroupActionLabel(string action) => action switch
    {
        GroupCapabilities.Announcements => "Group announcements",
        GroupCapabilities.Notice => "Noticeboard post",
        GroupCapabilities.Events => "Group events",
        _ => action
    };
    private static void DrawGroupPermissionChoices(GroupPermissionDraft draft, IEnumerable<string> actions)
    {
        foreach (var action in actions)
        {
            var enabled = draft.Actions.Contains(action);
            if (ImGui.Checkbox(GroupActionLabel(action), ref enabled)) draft.Set(action, enabled);
        }
    }
    private void ClearGroupPermissions()
    { groupFeaturesDraft.Clear(); groupStaffDraft.Clear(); groupStaffSelection = null; }

    private void DrawGroupFeatures(GroupProfile group)
    {
        var key = group.Id.ToString(); var actions = group.EnabledFeatures ?? [];
        groupFeaturesDraft.Refresh(key, group.Etag, actions);
        DrawGroupPermissionChoices(groupFeaturesDraft, GroupCapabilities.All);
        var stale = groupFeaturesDraft.ETag != group.Etag;
        if (stale) Wrapped("Group features changed. Reload before saving.");
        ImGui.BeginDisabled(stale || !groupFeaturesDraft.IsDirty);
        if (ActionButton("Save group features")) Mutate("PUT", GroupPath(group) + "/features", new GroupFeaturesEdit(groupFeaturesDraft.Actions.Order().ToArray()), groupFeaturesDraft.ETag);
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Reload features")) groupFeaturesDraft.Load(key, group.Etag, actions);
    }

    private void DrawGroupStaff(GroupProfile group)
    {
        LoadGroupPrivate(group);
        if (ActionButton("Refresh staff", mutation: false)) LoadGroupPrivate(group, true);
        if (groupPrivate == null) return;
        var members = groupPrivate.Members.Where(x => !x.IsOwner).ToArray();
        var selectedMember = members.FirstOrDefault(x => x.CharacterId == groupStaffSelection);
        if (ImGui.BeginCombo("Staff member", selectedMember == null ? "Choose a member" : selectedMember.Name + " @ " + selectedMember.World))
        {
            foreach (var member in members)
                if (ImGui.Selectable(member.Name + " @ " + member.World + "###staff" + member.CharacterId, member.CharacterId == groupStaffSelection))
                { groupStaffSelection = member.CharacterId; groupStaffDraft.Clear(); }
            ImGui.EndCombo();
        }
        selectedMember = members.FirstOrDefault(x => x.CharacterId == groupStaffSelection);
        if (groupPrivate.NextCursor != null && ActionButton("More group members", mutation: false)) LoadGroupPrivate(group, true, true);
        if (members.Length == 0) { Wrapped("Other members must join this group before you can assign staff permissions."); return; }
        if (selectedMember?.Etag == null) return;
        var key = group.Id + ":" + selectedMember.CharacterId;
        groupStaffDraft.Refresh(key, selectedMember.Etag, selectedMember.Capabilities ?? []);
        DrawGroupPermissionChoices(groupStaffDraft, group.EnabledFeatures ?? []);
        var stale = groupStaffDraft.ETag != selectedMember.Etag
            || groupStaffDraft.Actions.Any(x => group.EnabledFeatures?.Contains(x) != true);
        if (stale) Wrapped("Staff permissions changed. Reload before saving.");
        ImGui.BeginDisabled(stale || !groupStaffDraft.IsDirty);
        if (ActionButton("Save staff permissions")) Mutate("PUT", GroupPath(group) + "/members/" + selectedMember.CharacterId + "/capabilities", new GroupStaffEdit(groupStaffDraft.Actions.Order().ToArray()), groupStaffDraft.ETag);
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Reload staff permissions")) groupStaffDraft.Load(key, selectedMember.Etag, selectedMember.Capabilities ?? []);
    }
}
