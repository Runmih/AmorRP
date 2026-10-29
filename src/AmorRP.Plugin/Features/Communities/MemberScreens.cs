using System.Numerics;
using AmorRP.Contracts.Communities;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private string memberSearch = "", appliedMemberSearch = "";
    private Member[] memberSearchResults = [];
    private string? memberSearchCursor;
    private Member? SelectedMember => members.Concat(memberSearchResults).FirstOrDefault(x => x.Character.Id == memberSelection);
    private void SelectMember(Member row)
    {
        if (!members.Any(x => x.Character.Id == row.Character.Id)) members = [..members, row];
        inspectorRefresh.Reset();
        memberSelection = row.Character.Id; memberDraftEtag = row.Etag;
        ResetMemberDescription(row);
        LoadPersonalNote(row);
        grantCooking = row.Capabilities.Contains("items.cooking.create"); grantCookingDefinitions = row.Capabilities.Contains("items.cooking.define");
        grantGroupCreation = row.Capabilities.Contains("groups.create"); grantGroupManagement = row.Capabilities.Contains("groups.manage");
        grantGeneralModeration = row.Capabilities.Contains("community.moderate"); grantAnnouncements = row.Capabilities.Contains("announcements.manage"); grantGameplayPermissions = row.Capabilities.Contains("members.gameplay.manage");
        grantCoins = row.Capabilities.Contains("currency.manage"); grantPotions = row.Capabilities.Contains("items.potion.create"); grantRemoval = row.Capabilities.Contains("inventory.remove"); tradingAllowed = !row.TradingRestricted; grantDefinitions = row.Capabilities.Contains("items.potion.define");
        reason = removalReason = itemRemovalReason = delta = ""; removeItemsConfirmed = false;
        memberHoldings = []; memberInventoryFor = memberInventoryRequestedFor = memberHoldingSelection = null; memberHoldingCursor = null;
        ClearMemberBalance();
    }
    private void DrawMembers()
    {
        if (community == null) { Wrapped("Join or create a community first."); return; }
        DrawRefreshState(rosterRefresh, "Members");
        if (Owner && ImGui.Checkbox("Show inactive members", ref showInactive)) { members = []; memberSelection = null; ClearMemberSearch(); rosterRefresh.Invalidate(); refreshNeeded = true; }
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.CalcTextSize("Character description and private notes").X * 1.7f;
        if (!ImGui.BeginTable("MemberLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Members", ImGuiTableColumnFlags.WidthStretch, .38f); if (!narrow) ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, .62f);
        ImGui.TableNextColumn(); ImGui.BeginChild("MemberRoster", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 9 : 0), true);
        ImGui.SetNextItemWidth(-1); var searchNow = ImGui.InputTextWithHint("##MemberSearch", "Search all members (Enter)…", ref memberSearch, 201, ImGuiInputTextFlags.EnterReturnsTrue);
        if (searchNow || ImGui.IsItemDeactivatedAfterEdit()) SearchMembers();
        if (appliedMemberSearch.Length > 0 && ImGui.SmallButton("Clear search")) { memberSearch = ""; SearchMembers(); }
        var visibleMembers = appliedMemberSearch.Length > 0 ? memberSearchResults : members;
        if (appliedMemberSearch.Length > 0 && visibleMembers.Length == 0) Wrapped("No members found.");
        foreach (var row in visibleMembers)
        {
            ImGui.PushID(row.Character.Id.ToString());
            if (ImGui.Selectable(row.Character.DisplayName + "###Member", memberSelection == row.Character.Id)) SelectMember(row);
            ImGui.TextDisabled(row.Character.HomeWorldName + " · " + (row.IsOwner ? "owner" : row.Status == "dormant" ? "inactive" : row.Status)); ImGui.PopID();
        }
        if ((appliedMemberSearch.Length > 0 ? memberSearchCursor : membersCursor) != null && ActionButton("More members", mutation: false))
            SearchMembers(more: true);
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("SelectedMember", Vector2.Zero, true); ImGui.PushItemWidth(-1);
        var target = SelectedMember;
        if (target == null) Wrapped("Select a member on the left. Their available actions appear here.");
        else
        {
            ImGui.TextUnformatted(target.Character.DisplayName);
            DrawMemberDescription(target);
            if (target.Status == "active" && target.Character.Id != session!.Character.Id && !target.TradingRestricted && !community.MyTradingRestricted && ImGui.Button("Trade with this member"))
            { contextTarget = null; tradeTargetId = target.Character.Id; requestedTradeTab = 1; OpenTradeWindow?.Invoke(); }
            DrawPersonalNote(target);
            if (CanManageGameplay && target.Status == "active")
            {
                if (target.IsOwner)
                {
                    ImGui.Separator(); ImGui.TextUnformatted("Gameplay permissions");
                    ImGui.TextDisabled("Community owner — all permissions enabled.");
                }
                else DrawMemberAuthorizations(target);
            }
            if (Owner && target.Status == "active" && !target.IsOwner) DrawCommunityPermissions(target);
            if (CanCoins && target.Status == "active")
            {
                EnsureMemberBalance(target);
                if (ImGui.CollapsingHeader("Currency")) DrawMemberCurrency(target);
            }
            if ((Owner || community.MyCapabilities.Contains("inventory.remove")) && ImGui.CollapsingHeader("Manage items")) DrawMemberInventory(target);
            if (Owner && !target.IsOwner && ImGui.CollapsingHeader("Manage membership")) DrawMemberMembership(target);
        }
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
    private void DrawMemberAuthorizations(Member target)
    {
        if (community == null) return;
        ImGui.Separator(); ImGui.TextUnformatted("Gameplay permissions");
        DrawPermissionConflict(target);
        ImGui.Checkbox("Trading", ref tradingAllowed);
        ImGui.Checkbox("Craft Alchemy Items", ref grantPotions);
        ImGui.Checkbox("Craft Cooking Items", ref grantCooking);
        ImGui.BeginDisabled(memberDraftEtag != target.Etag);
        if (ActionButton("Save permissions"))
        {
            // Preserve moderator grants; this form only changes gameplay actions.
            var actions = target.Capabilities.Where(x => x is not ("items.potion.create" or "items.cooking.create")).ToList();
            if (grantPotions) actions.Add("items.potion.create");
            if (grantCooking) actions.Add("items.cooking.create");
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
        Mutate("PUT", $"api/v2/communities/{community!.Id}/members/{target.Character.Id}/capabilities", permissions, memberDraftEtag ?? target.Etag);
    }
    private void DrawCommunityPermissions(Member target)
    {
        if (!ImGui.CollapsingHeader("Community permissions")) return;
        DrawPermissionConflict(target);
        ImGui.TextUnformatted("Moderation");
        ImGui.Checkbox("General Moderation", ref grantGeneralModeration);
        ImGui.Checkbox("Manage Gameplay Permissions", ref grantGameplayPermissions);
        ImGui.Checkbox("Create Announcements", ref grantAnnouncements);

        ImGui.Separator(); ImGui.TextUnformatted("Recipe creation");
        ImGui.Checkbox("Create Alchemy Recipes", ref grantDefinitions);
        ImGui.Checkbox("Create Cooking Recipes", ref grantCookingDefinitions);
        ImGui.Separator(); ImGui.TextUnformatted("Group administration");
        ImGui.Checkbox("Create Groups", ref grantGroupCreation);
        ImGui.Checkbox("Manage Groups", ref grantGroupManagement);
        ImGui.Separator(); ImGui.TextUnformatted("Economy");
        ImGui.Checkbox("Manage currency", ref grantCoins);
        ImGui.Checkbox("Remove other members' items", ref grantRemoval);
        ImGui.BeginDisabled(memberDraftEtag != target.Etag);
        if (ActionButton("Save moderator permissions"))
        {
            // Do not silently save or discard an unfinished gameplay draft.
            var actions = target.Capabilities.Where(x => x is not ("currency.manage" or "inventory.remove" or "items.potion.define" or "community.moderate" or "announcements.manage" or "members.gameplay.manage" or "groups.create" or "groups.manage" or "items.cooking.define")).ToList();
            if (grantGroupCreation) actions.Add("groups.create");
            if (grantGroupManagement) actions.Add("groups.manage");
            if (grantGeneralModeration) actions.Add("community.moderate");
            if (grantGameplayPermissions) actions.Add("members.gameplay.manage");
            if (grantAnnouncements) actions.Add("announcements.manage");
            if (grantCoins) actions.Add("currency.manage");
            if (grantRemoval) actions.Add("inventory.remove");
            if (grantDefinitions) actions.Add("items.potion.define");
            if (grantCookingDefinitions) actions.Add("items.cooking.define");
            SaveMemberPermissions(target, new MemberCapabilities(actions.ToArray()));
        }
        ImGui.EndDisabled();
    }
    private void DrawMemberMembership(Member target)
    {
        if (community == null) return;
        if (target.Status == "active")
        {
            ImGui.Separator(); ImGui.TextUnformatted("Membership actions");
            FieldLabel("Reason (required)"); LimitedInput("##MemberRemovalReason", ref removalReason, 500, countCodeUnits: true);
            var validReason = !string.IsNullOrWhiteSpace(removalReason) && removalReason.Length <= 500;
            if (ShiftMemberButton("Remove member", validReason))
                Mutate("DELETE", $"api/v2/communities/{community.Id}/members/{target.Character.Id}", new MemberRemoval(removalReason), memberDraftEtag ?? target.Etag);
            if (ShiftMemberButton("Block member from rejoining", validReason))
                Mutate("PUT", $"api/v2/communities/{community.Id}/members/{target.Character.Id}/block", new MemberRemoval(removalReason), memberDraftEtag ?? target.Etag);
            if (ShiftMemberButton("Propose ownership transfer"))
                Mutate("POST", $"api/v2/communities/{community.Id}/ownership-transfers", new OwnershipProposal(target.Character.Id));
        }
        else if (target.Status == "dormant")
        {
            FieldLabel("Block reason (required)"); LimitedInput("##DormantBlockReason", ref removalReason, 500, countCodeUnits: true);
            if (ShiftMemberButton("Block member from rejoining", !string.IsNullOrWhiteSpace(removalReason) && removalReason.Length <= 500))
                Mutate("PUT", $"api/v2/communities/{community.Id}/members/{target.Character.Id}/block", new MemberRemoval(removalReason), target.Etag);
        }
        else if (target.Status == "blocked")
        {
            FieldLabel("Unblock reason (required)"); LimitedInput("##MemberUnblockReason", ref reason, 500, countCodeUnits: true);
            if (ShiftMemberButton("Unblock member", !string.IsNullOrWhiteSpace(reason) && reason.Length <= 500))
                Mutate("DELETE", $"api/v2/communities/{community.Id}/members/{target.Character.Id}/block", new MemberRemoval(reason), memberDraftEtag ?? target.Etag);
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
