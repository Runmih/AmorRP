using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly GroupPermissionDraft groupFeaturesDraft = new(), groupStaffDraft = new();
    private Guid? groupStaffSelection;
    private string? staffCommissionKey;
    private int? staffCommission;
    private bool CanGroupAction(GroupProfile group, string action) => group.IsMember
        && GroupCapabilities.Enabled(group.EnabledFeatures ?? [], action)
        && (group.OwnerCharacterId == session?.Character.Id || group.MyCapabilities?.Contains(action) == true);

    private static string GroupActionLabel(string action) => action switch
    {
        GroupCapabilities.Announcements => "Group announcements",
        GroupCapabilities.Notice => "Noticeboard post",
        GroupCapabilities.Events => "Group events",
        GroupCapabilities.Billing => "Bill trusted customers",
        GroupCapabilities.Discord => "Discord Bot Permission",
        GroupCapabilities.Storage => "View and deposit group items",
        GroupCapabilities.StorageWithdraw => "Withdraw group items",
        GroupCapabilities.Treasury => "Currency treasury",
        GroupCapabilities.TreasuryRecords => "View treasury records",
        GroupCapabilities.TreasuryWithdraw => "Withdraw treasury currency",
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
    { groupFeaturesDraft.Clear(); groupStaffDraft.Clear(); groupStaffSelection = null; staffCommissionKey = null; }

    private void DrawGroupFeatures(GroupProfile group)
    {
        var key = group.Id.ToString(); var actions = group.EnabledFeatures ?? [];
        groupFeaturesDraft.Refresh(key, group.Etag, actions);
        DrawGroupPermissionChoices(groupFeaturesDraft, GroupCapabilities.Features);
        var stale = groupFeaturesDraft.ETag != group.Etag;
        if (stale) Wrapped("Group features changed. Reload before saving.");
        ImGui.BeginDisabled(stale || !groupFeaturesDraft.IsDirty);
        if (ActionButton("Save group features")) Mutate("PUT", GroupPath(group) + "/features", new GroupFeaturesEdit(groupFeaturesDraft.Actions.Order().ToArray()), groupFeaturesDraft.ETag);
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Reload features")) groupFeaturesDraft.Load(key, group.Etag, actions);
    }

    private void DrawGroupMemberPermissions(GroupProfile group, GroupMember selectedMember)
    {
        if (selectedMember.Etag == null) return;
        ImGui.Separator();
        FieldLabel(selectedMember.Name);
        if (selectedMember.ClockedInAt is { } clockedIn)
        {
            Wrapped($"Clocked in since {clockedIn.LocalDateTime:g}");
            if (ActionButton("Clock this member out"))
                Mutate("PUT", GroupPath(group) + "/clock-in", new AmorRP.Contracts.Currency.ClockInEdit(false, selectedMember.CharacterId));
        }
        var key = group.Id + ":" + selectedMember.CharacterId;
        groupStaffDraft.Refresh(key, selectedMember.Etag, selectedMember.Capabilities ?? []);
        DrawGroupPermissionChoices(groupStaffDraft, GroupCapabilities.All.Where(x => GroupCapabilities.Enabled(group.EnabledFeatures ?? [], x)));
        var commissionKey = key + ":" + selectedMember.Etag;
        if (staffCommissionKey != commissionKey)
        {
            staffCommissionKey = commissionKey;
            staffCommission = selectedMember.CommissionPercent;
        }
        if (GroupCapabilities.Enabled(group.EnabledFeatures ?? [], GroupCapabilities.Billing))
        {
            var custom = staffCommission.HasValue;
            if (ImGui.Checkbox("Individual commission", ref custom)) staffCommission = custom ? 0 : null;
            if (staffCommission is { } rate && ImGui.InputInt("Commission after tax (%)", ref rate))
                staffCommission = Math.Clamp(rate, 0, 100);
        }
        var stale = groupStaffDraft.ETag != selectedMember.Etag
            || groupStaffDraft.Actions.Any(x => !GroupCapabilities.Enabled(group.EnabledFeatures ?? [], x));
        if (stale) Wrapped("Staff permissions changed. Reload before saving.");
        ImGui.BeginDisabled(stale || (!groupStaffDraft.IsDirty && staffCommission == selectedMember.CommissionPercent));
        if (ActionButton("Save staff permissions")) Mutate("PUT", GroupPath(group) + "/members/" + selectedMember.CharacterId + "/capabilities", new GroupStaffEdit(groupStaffDraft.Actions.Order().ToArray(), staffCommission, true), groupStaffDraft.ETag);
        ImGui.EndDisabled(); ImGui.SameLine();
        if (ImGui.Button("Reload staff permissions"))
        {
            groupStaffDraft.Load(key, selectedMember.Etag, selectedMember.Capabilities ?? []);
            staffCommission = selectedMember.CommissionPercent;
        }
    }
}
