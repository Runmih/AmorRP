using AmorRP.Contracts.Groups;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private void DrawGroupSettings()
    {
        if (!Owner || group == null || !ImGui.CollapsingHeader("Group Settings")) return;
        if (!ImGui.BeginTabBar("GroupSettingsTabs")) return;
        if (ImGui.BeginTabItem("Currency")) { DrawCurrencyIconEditor(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Invitations")) { DrawInvitations(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Delete Group"))
        {
            Wrapped("Delete makes this group's assets inaccessible to all members. Enter the exact group name to confirm.");
            FieldLabel("Exact group name"); ImGui.InputText("##DeleteGroupName", ref deleteConfirmation, 256);
            ImGui.BeginDisabled(deleteConfirmation != group.Name);
            if (ActionButton("Delete this group")) Mutate("POST", $"api/v1/groups/{group.Id}/deletion", new GroupDeletion(deleteConfirmation), ETag("group", group.Id, group.Version));
            ImGui.EndDisabled(); ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
        if (transfer is { Status: "proposed" })
        {
            Wrapped("An ownership proposal is pending.");
            if (ActionButton("Cancel ownership proposal")) Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers/{transfer.Id}/cancel", null);
        }
    }
    private void DrawInvitations()
    {
        FieldLabel("Valid for hours (1–168)"); ImGui.InputInt("##InviteHours", ref inviteHours);
        FieldLabel("Maximum uses (1–100)"); ImGui.InputInt("##InviteUses", ref inviteUses);
        if (ActionButton("Create invitation")) Mutate("POST", $"api/v1/groups/{group!.Id}/invitations", new InvitationCreate(inviteHours, inviteUses));
        if (createdCode.Length > 0) { FieldLabel("New invitation code"); ImGui.InputText("##CreatedInvite", ref createdCode, 100, ImGuiInputTextFlags.ReadOnly); }
        foreach (var invite in invitations)
        {
            ImGui.PushID(invite.Id.ToString());
            Wrapped($"{invite.Used}/{invite.MaxUses} uses; expires {invite.ExpiresAt.LocalDateTime:g}{(invite.Revoked ? " — revoked" : "")}");
            if (!invite.Revoked && ActionButton("Revoke invitation")) Mutate("DELETE", $"api/v1/groups/{group!.Id}/invitations/{invite.Id}", null);
            ImGui.PopID();
        }
        if (invitationsCursor != null && ActionButton("More invitations", mutation: false))
        {
            var path = $"api/v1/groups/{group!.Id}/invitations?cursor={Uri.EscapeDataString(invitationsCursor)}"; var token = session!.AccessToken;
            Start(async ct => { var page = await client.GetAsync<InvitationPage>(origin!, path, token, ct); return () => { invitations = [..invitations, ..page.Items]; invitationsCursor = page.NextCursor; }; }, "Loading invitations…");
        }
    }
}
