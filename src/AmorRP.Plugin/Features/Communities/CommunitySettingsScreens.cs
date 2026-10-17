using AmorRP.Contracts.Communities;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private void DrawCommunitySettings()
    {
        if (!Owner || community == null || !ImGui.CollapsingHeader("Community Settings")) return;
        if (!ImGui.BeginTabBar("CommunitySettingsTabs")) return;
        if (ImGui.BeginTabItem("Currency")) { DrawCurrencyIconEditor(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Invitations")) { DrawInvitations(); ImGui.EndTabItem(); }
        if (ImGui.BeginTabItem("Delete Community"))
        {
            Wrapped("Delete makes this community's assets inaccessible to all members. Enter the exact community name to confirm.");
            FieldLabel("Exact community name"); ImGui.InputText("##DeleteCommunityName", ref deleteConfirmation, 256);
            ImGui.BeginDisabled(deleteConfirmation != community.Name);
            if (ActionButton("Delete this community")) Mutate("POST", $"api/v2/communities/{community.Id}/deletion", new CommunityDeletion(deleteConfirmation), ETag("community", community.Id, community.Version));
            ImGui.EndDisabled(); ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
        if (transfer is { Status: "proposed" })
        {
            Wrapped("An ownership proposal is pending.");
            if (ActionButton("Cancel ownership proposal")) Mutate("POST", $"api/v2/communities/{community.Id}/ownership-transfers/{transfer.Id}/cancel", null);
        }
    }
    private void DrawInvitations()
    {
        Wrapped("Invitation codes stay valid until you delete them. Share them only with people you want to join.");
        if (ActionButton("Create invitation")) Mutate("POST", $"api/v2/communities/{community!.Id}/invitations", new InvitationCreate());
        if (createdCode.Length > 0) { FieldLabel("New invitation code"); ImGui.InputText("##CreatedInvite", ref createdCode, 100, ImGuiInputTextFlags.ReadOnly); if (ImGui.Button("Copy code")) { ImGui.SetClipboardText(createdCode); actionResult = "Invitation code copied."; } }
        foreach (var invite in invitations.Where(x => !x.Revoked && x.ExpiresAt > DateTimeOffset.UtcNow && x.Used < x.MaxUses))
        {
            ImGui.PushID(invite.Id.ToString());
            Wrapped($"Invitation {invite.Id.ToString()[^6..].ToUpperInvariant()} · {invite.Used} joined · no expiry");
            if (!invite.Revoked && ActionButton("Delete invitation")) Mutate("DELETE", $"api/v2/communities/{community!.Id}/invitations/{invite.Id}", null);
            ImGui.PopID();
        }
        if (invitationsCursor != null && ActionButton("More invitations", mutation: false))
        {
            var path = $"api/v2/communities/{community!.Id}/invitations?cursor={Uri.EscapeDataString(invitationsCursor)}"; var token = session!.AccessToken;
            Start(async ct => { var page = await client.GetAsync<InvitationPage>(origin!, path, token, ct); return () => { invitations = [..invitations, ..page.Items]; invitationsCursor = page.NextCursor; }; }, "Loading invitations…");
        }
    }
}
