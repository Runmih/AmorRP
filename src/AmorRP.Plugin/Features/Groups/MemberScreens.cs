using System.Numerics;
using AmorRP.Contracts.Groups;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private string memberSearch = "";
    private Member? SelectedMember => members.FirstOrDefault(x => x.Character.Id == memberSelection);
    private void SelectMember(Member row)
    {
        memberSelection = row.Character.Id; memberDraftEtag = row.Etag;
        ResetMemberDescription(row);
        grantGeneralModeration = row.Capabilities.Contains("group.moderate"); grantAnnouncements = row.Capabilities.Contains("announcements.manage"); grantGameplayPermissions = row.Capabilities.Contains("members.gameplay.manage");
        grantCoins = row.Capabilities.Contains("currency.manage"); grantPotions = row.Capabilities.Contains("items.potion.create"); grantRemoval = row.Capabilities.Contains("inventory.remove"); tradingAllowed = !row.TradingRestricted; grantDefinitions = row.Capabilities.Contains("items.potion.define");
        reason = removalReason = itemRemovalReason = delta = ""; removeItemsConfirmed = false;
        memberHoldings = []; memberInventoryFor = memberInventoryRequestedFor = memberHoldingSelection = null; memberHoldingCursor = null;
        ClearMemberBalance();
    }
    private void DrawMembers()
    {
        if (group == null) { Wrapped("Join or create a group first."); return; }
        if (Owner && ImGui.Checkbox("Show inactive members", ref showInactive)) { members = []; memberSelection = null; refreshNeeded = true; }
        if (!ImGui.BeginTable("MemberLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Members", ImGuiTableColumnFlags.WidthStretch, .38f); ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, .62f);
        ImGui.TableNextColumn(); ImGui.BeginChild("MemberRoster", Vector2.Zero, true);
        ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##MemberSearch", "Filter loaded members…", ref memberSearch, 400);
        foreach (var row in members.Where(x => (x.Character.DisplayName + " " + x.Character.HomeWorldName).Contains(memberSearch, StringComparison.OrdinalIgnoreCase)))
        {
            ImGui.PushID(row.Character.Id.ToString());
            if (ImGui.Selectable(row.Character.DisplayName + "###Member", memberSelection == row.Character.Id)) SelectMember(row);
            ImGui.TextDisabled(row.Character.HomeWorldName + " · " + (row.IsOwner ? "owner" : row.Status == "dormant" ? "inactive" : row.Status)); ImGui.PopID();
        }
        if (membersCursor != null && ActionButton("More members", mutation: false))
        {
            var cursor = membersCursor; var id = group.Id; var token = session!.AccessToken; var owner = Owner && showInactive;
            Start(async ct => { var page = await client.GetAsync<MemberPage>(origin!, $"api/v1/groups/{id}/members?cursor={Uri.EscapeDataString(cursor)}" + (owner ? "&includeDormant=true" : ""), token, ct);
                return () => { members = members.Concat(page.Items).DistinctBy(x => x.Character.Id).ToArray(); membersCursor = page.NextCursor; }; }, "Loading more members…");
        }
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("SelectedMember", Vector2.Zero, true); ImGui.PushItemWidth(-1);
        var target = SelectedMember;
        if (target == null) Wrapped("Select a member on the left. Their available actions appear here.");
        else
        {
            ImGui.TextUnformatted(target.Character.DisplayName);
            DrawMemberDescription(target);
            if (target.Status == "active" && target.Character.Id != session!.Character.Id && !target.TradingRestricted && !group.MyTradingRestricted && ImGui.Button("Trade with this member"))
            { contextTarget = null; tradeTargetId = target.Character.Id; requestedTradeTab = 1; OpenTradeWindow?.Invoke(); }
            if (CanManageGameplay && target.Status == "active")
            {
                if (target.IsOwner)
                {
                    ImGui.Separator(); ImGui.TextUnformatted("Permissions");
                    ImGui.TextDisabled("Group owner — all permissions enabled.");
                }
                else DrawMemberAuthorizations(target);
            }
            if (CanCoins && target.Status == "active")
            {
                EnsureMemberBalance(target);
                if (ImGui.CollapsingHeader("Currency")) DrawMemberCurrency(target);
            }
            if ((Owner || group.MyCapabilities.Contains("inventory.remove")) && ImGui.CollapsingHeader("Manage items")) DrawMemberInventory(target);
            if (Owner && !target.IsOwner && ImGui.CollapsingHeader("Manage membership")) DrawMemberMembership(target);
        }
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
    private void DrawMemberAuthorizations(Member target)
    {
        if (group == null) return;
        ImGui.Separator(); ImGui.TextUnformatted("Permissions");
        DrawPermissionConflict(target);
        ImGui.Checkbox("Trading", ref tradingAllowed);
        ImGui.Checkbox("Craft Potions", ref grantPotions);
        ImGui.BeginDisabled(memberDraftEtag != target.Etag);
        if (ActionButton("Save permissions"))
        {
            // Preserve moderator grants; this form only changes gameplay actions.
            var actions = target.Capabilities.Where(x => x != "items.potion.create").ToList();
            if (grantPotions) actions.Add("items.potion.create");
            SaveMemberPermissions(target, new MemberCapabilities(actions.ToArray(), tradingAllowed));
        }
        ImGui.EndDisabled(); DrawAuthorizationFeedback(target);
    }
    private void DrawPermissionConflict(Member target)
    {
        if (memberDraftEtag == target.Etag) return;
        Wrapped("Permissions changed. Reload before saving.");
        if (ActionButton("Reload permissions", mutation: false)) ReloadSavedPermissions(target);
    }
    private void SaveMemberPermissions(Member target, MemberCapabilities permissions)
    {
        authorizationResultFor = target.Character.Id; authorizationResult = "Saving permissions…"; authorizationDiagnostic = "";
        Mutate("PUT", $"api/v1/groups/{group!.Id}/members/{target.Character.Id}/capabilities", permissions, memberDraftEtag ?? target.Etag);
    }
    private void DrawMemberMembership(Member target)
    {
        if (group == null) return;
        if (target.Status == "active")
        {
            ImGui.TextUnformatted("Moderator permissions");
            ImGui.Checkbox("General Moderation", ref grantGeneralModeration);
            ImGui.Checkbox("Manage Gameplay Permissions", ref grantGameplayPermissions);
            ImGui.Checkbox("Create Announcements", ref grantAnnouncements);
            ImGui.Checkbox("Potion Creation (define recipes)", ref grantDefinitions);
            ImGui.Checkbox("Manage currency", ref grantCoins);
            ImGui.Checkbox("Remove other members' items", ref grantRemoval);
            ImGui.BeginDisabled(memberDraftEtag != target.Etag);
            if (ActionButton("Save moderator permissions"))
            {
                // Do not silently save or discard an unfinished gameplay draft.
                var actions = target.Capabilities.Where(x => x is not ("currency.manage" or "inventory.remove" or "items.potion.define" or "group.moderate" or "announcements.manage" or "members.gameplay.manage")).ToList();
                if (grantGeneralModeration) actions.Add("group.moderate");
                if (grantGameplayPermissions) actions.Add("members.gameplay.manage");
                if (grantAnnouncements) actions.Add("announcements.manage");
                if (grantCoins) actions.Add("currency.manage");
                if (grantRemoval) actions.Add("inventory.remove");
                if (grantDefinitions) actions.Add("items.potion.define");
                SaveMemberPermissions(target, new MemberCapabilities(actions.ToArray()));
            }
            ImGui.EndDisabled();
            ImGui.Separator(); ImGui.TextUnformatted("Membership actions");
            FieldLabel("Reason (required)"); LimitedInput("##MemberRemovalReason", ref removalReason, 500, countCodeUnits: true);
            var validReason = !string.IsNullOrWhiteSpace(removalReason) && removalReason.Length <= 500;
            if (ShiftMemberButton("Remove member", validReason))
                Mutate("DELETE", $"api/v1/groups/{group.Id}/members/{target.Character.Id}", new MemberRemoval(removalReason), memberDraftEtag ?? target.Etag);
            if (ShiftMemberButton("Block member from rejoining", validReason))
                Mutate("PUT", $"api/v1/groups/{group.Id}/members/{target.Character.Id}/block", new MemberRemoval(removalReason), memberDraftEtag ?? target.Etag);
            if (ShiftMemberButton("Propose ownership transfer"))
                Mutate("POST", $"api/v1/groups/{group.Id}/ownership-transfers", new OwnershipProposal(target.Character.Id));
        }
        else if (target.Status == "dormant")
        {
            FieldLabel("Block reason (required)"); LimitedInput("##DormantBlockReason", ref removalReason, 500, countCodeUnits: true);
            if (ShiftMemberButton("Block member from rejoining", !string.IsNullOrWhiteSpace(removalReason) && removalReason.Length <= 500))
                Mutate("PUT", $"api/v1/groups/{group.Id}/members/{target.Character.Id}/block", new MemberRemoval(removalReason), target.Etag);
        }
        else if (target.Status == "blocked")
        {
            FieldLabel("Unblock reason (required)"); LimitedInput("##MemberUnblockReason", ref reason, 500, countCodeUnits: true);
            if (ShiftMemberButton("Unblock member", !string.IsNullOrWhiteSpace(reason) && reason.Length <= 500))
                Mutate("DELETE", $"api/v1/groups/{group.Id}/members/{target.Character.Id}/block", new MemberRemoval(reason), memberDraftEtag ?? target.Etag);
        }
    }
    private bool ShiftMemberButton(string label, bool valid = true)
    {
        ImGui.BeginDisabled(!ImGui.GetIO().KeyShift || !valid);
        var clicked = ActionButton(label);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(valid ? "Hold Shift to enable this button." : "Enter a reason (1–500 characters), then hold Shift to enable this button.");
        return clicked;
    }
}
