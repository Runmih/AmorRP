using System.Numerics;
using AmorRP.Contracts.Seasons;
using AmorRP.Core.Items;
using AmorRP.Core.Combat;
using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private enum MemberView { Directory, Management }
    private Guid? pendingMemberSelection;
    private string memberSearch = "", appliedMemberSearch = "";
    private Member[] memberSearchResults = [];
    private string? memberSearchCursor;
    private Member? SelectedMember => members.Concat(memberSearchResults).FirstOrDefault(x => x.Character.Id == memberSelection);
    private void SelectMember(Member row)
    {
        if (!members.Any(x => x.Character.Id == row.Character.Id)) members = [..members, row];
        inspectorRefresh.Reset();
        memberSelection = row.Character.Id; memberDraftEtag = row.Etag;
        LoadPersonalNote(row);
        grantGathering = row.Capabilities.Contains(CraftingTypes.GatherPermission);
        grantBlacksmithing = row.Capabilities.Contains(CraftingTypes.BlacksmithPermission);
        grantBlacksmithDefinitions = row.Capabilities.Contains(CraftingTypes.BlacksmithRecipePermission);
        combatAllowed = !row.CombatRestricted;
        staffHp = CombatRules.MaxHitPoints;
        grantCooking = row.Capabilities.Contains("items.cooking.create"); grantCookingDefinitions = row.Capabilities.Contains("items.cooking.define");
        grantPermitListings = row.Capabilities.Contains(ManagePermitListings);
        grantGroupCreation = row.Capabilities.Contains("groups.create"); grantGroupManagement = row.Capabilities.Contains("groups.manage");
        grantGeneralModeration = row.Capabilities.Contains("season.moderate"); grantAnnouncements = row.Capabilities.Contains("announcements.manage"); grantGameplayPermissions = row.Capabilities.Contains("members.gameplay.manage");
        grantCoins = row.Capabilities.Contains("currency.manage"); grantPotions = row.Capabilities.Contains("items.potion.create"); grantRemoval = row.Capabilities.Contains("inventory.remove"); tradingAllowed = !row.TradingRestricted; grantDefinitions = row.Capabilities.Contains("items.potion.define");
        reason = itemRemovalReason = delta = ""; removeItemsConfirmed = false;
        memberHoldings = []; memberInventoryFor = memberInventoryRequestedFor = memberHoldingSelection = null; memberHoldingCursor = null;
        ClearMemberBalance();
    }
    private void DrawMembers(MemberView view = MemberView.Directory)
    {
        if (community == null) { DrawNoSeason(); return; }
        DrawRefreshState(rosterRefresh, "Members");
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.CalcTextSize("Character description and private notes").X * 1.7f;
        if (!ImGui.BeginTable("MemberLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Members", ImGuiTableColumnFlags.WidthStretch, .38f); if (!narrow) ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, .62f);
        ImGui.TableNextColumn(); ImGui.BeginChild("MemberRoster", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 9 : 0), true);
        UiIcons.SearchLabel();
        ImGui.SetNextItemWidth(Math.Max(70, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 1.6f));
        var searchNow = ImGui.InputTextWithHint("##MemberSearch", "Search all members (Enter)…", ref memberSearch, 201, ImGuiInputTextFlags.EnterReturnsTrue);
        if (searchNow || ImGui.IsItemDeactivatedAfterEdit()) SearchMembers();
        ImGui.SameLine();
        ImGui.BeginDisabled(memberSearch.Length == 0 && appliedMemberSearch.Length == 0);
        if (UiIcons.ClearButton("Clear member search")) { memberSearch = ""; SearchMembers(); }
        ImGui.EndDisabled();
        var visibleMembers = (appliedMemberSearch.Length > 0 ? memberSearchResults : members)
            .Where(x => view != MemberView.Directory || x.Status == "active").ToArray();
        if (appliedMemberSearch.Length > 0 && visibleMembers.Length == 0) Wrapped("No members found.");
        Member? clickedMember = null;
        foreach (var row in visibleMembers)
        {
            ImGui.PushID(row.Character.Id.ToString());
            if (ImGui.Selectable(row.Character.DisplayName + "###Member", memberSelection == row.Character.Id)) clickedMember = row;
            ImGui.PopID();
        }
        if (clickedMember != null) RequestMemberSelection(clickedMember);
        DrawMemberSwitchDialog();
        if ((appliedMemberSearch.Length > 0 ? memberSearchCursor : membersCursor) != null && ActionButton("More members", mutation: false))
            SearchMembers(more: true);
        ImGui.EndChild(); ImGui.TableNextColumn(); ImGui.BeginChild("SelectedMember", Vector2.Zero, true); ImGui.PushItemWidth(-1);
        var target = SelectedMember;
        if (view == MemberView.Directory && target?.Status != "active") target = null;
        if (target == null) Wrapped("Select a member on the left. Their available actions appear here.");
        else
        {
            ImGui.TextUnformatted(target.Character.DisplayName);
            if (view != MemberView.Management)
            {
                if (target.Description.Length > 0) Wrapped(target.Description);
                if (!SeasonReadOnly && target.Status == "active" && target.Character.Id != session!.Character.Id && !target.TradingRestricted && !community.MyTradingRestricted && ImGui.Button("Trade with this member"))
                { if (BeginTradeRequest()) SendTradeRequest(target); }
                if (target.Character.Id != session!.Character.Id && !SeasonReadOnly && ImGui.Button("Select combat target"))
                {
                    combatTarget = target.Character.Id;
                    OpenCombatActions?.Invoke();
                }
                DrawPersonalNote(target);
            }
            if (CanNominatePermit && target.Status == "active" && ActionButton("Request Slave listing")) NominatePermit(target);
            if (view == MemberView.Directory && CanManageCommunityMembers && ImGui.Button("Manage this member"))
            { requestedManagementMembers = true; OpenWorkspaceWindow?.Invoke(WorkspaceKind.CommunityManagement); }
            if (view != MemberView.Directory && CanManageGameplay && target.Status == "active")
            {
                if (target.IsOwner)
                {
                    ImGui.Separator(); ImGui.TextUnformatted("Gameplay permissions");
                    ImGui.TextDisabled("Season owner — all permissions enabled.");
                }
                else DrawMemberAuthorizations(target);
            }
            if (view == MemberView.Management && CanGeneralModerate && target.Status == "active") DrawCombatHealthAdmin(target);
            if (view != MemberView.Directory && Owner && target.Status == "active" && !target.IsOwner) DrawCommunityPermissions(target);
            if (view != MemberView.Directory && CanCoins && target.Status == "active")
            {
                EnsureMemberBalance(target);
                if (ImGui.CollapsingHeader("Currency")) DrawMemberCurrency(target);
            }
            if (view != MemberView.Directory && (Owner || community.MyCapabilities.Contains("inventory.remove")) && ImGui.CollapsingHeader("Manage items")) DrawMemberInventory(target);
            if (view != MemberView.Directory && Owner && !target.IsOwner && ImGui.CollapsingHeader("Ownership")) DrawMemberOwnershipTransfer(target);
        }
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }
    private bool HasMemberDrafts()
    {
        var current = SelectedMember;
        if (current == null) return false;
        var caps = current.Capabilities;
        return personalNote != savedPersonalNote
            || delta.Length > 0 || reason.Length > 0 || itemRemovalReason.Length > 0
            || tradingAllowed == current.TradingRestricted || grantCoins != caps.Contains("currency.manage")
            || grantGathering != caps.Contains(CraftingTypes.GatherPermission)
            || grantBlacksmithing != caps.Contains(CraftingTypes.BlacksmithPermission)
            || grantBlacksmithDefinitions != caps.Contains(CraftingTypes.BlacksmithRecipePermission)
            || combatAllowed == current.CombatRestricted
            || grantPotions != caps.Contains("items.potion.create") || grantCooking != caps.Contains("items.cooking.create")
            || grantDefinitions != caps.Contains("items.potion.define") || grantCookingDefinitions != caps.Contains("items.cooking.define")
            || grantRemoval != caps.Contains("inventory.remove") || grantGeneralModeration != caps.Contains("season.moderate")
            || grantAnnouncements != caps.Contains("announcements.manage") || grantGameplayPermissions != caps.Contains("members.gameplay.manage")
            || grantPermitListings != caps.Contains(ManagePermitListings)
            || grantGroupCreation != caps.Contains("groups.create") || grantGroupManagement != caps.Contains("groups.manage");
    }
    private void RequestMemberSelection(Member member)
    {
        if (memberSelection == member.Character.Id) return;
        if (!HasMemberDrafts()) { SelectMember(member); return; }
        pendingMemberSelection = member.Character.Id; ImGui.OpenPopup("Switch member and discard drafts?");
    }
    private void DrawMemberSwitchDialog()
    {
        if (!ImGui.BeginPopupModal("Switch member and discard drafts?", ImGuiWindowFlags.AlwaysAutoResize)) return;
        Wrapped("You have unfinished member changes or private notes. Switching will discard those drafts.");
        if (ImGui.Button("Discard drafts and switch"))
        {
            var next = members.Concat(memberSearchResults).FirstOrDefault(x => x.Character.Id == pendingMemberSelection);
            if (next != null) SelectMember(next);
            pendingMemberSelection = null; ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine(); if (ImGui.Button("Keep editing")) { pendingMemberSelection = null; ImGui.CloseCurrentPopup(); }
        ImGui.EndPopup();
    }
    private void DrawMemberAuthorizations(Member target)
    {
        if (community == null) return;
        ImGui.Separator(); ImGui.TextUnformatted("Gameplay permissions");
        DrawPermissionConflict(target);
        ImGui.Checkbox("Trading", ref tradingAllowed);
        ImGui.Checkbox("Combat attacks", ref combatAllowed);
        ImGui.Checkbox("Craft Alchemy Items", ref grantPotions);
        ImGui.Checkbox("Craft Cooking Items", ref grantCooking);
        ImGui.Checkbox("Gathering and Hunting", ref grantGathering);
        ImGui.Checkbox("Blacksmithing", ref grantBlacksmithing);
        ImGui.BeginDisabled(memberDraftEtag != target.Etag);
        if (ActionButton("Save permissions"))
        {
            // Preserve moderator grants; this form only changes gameplay actions.
            var actions = target.Capabilities.Where(x => x is not ("items.potion.create" or "items.cooking.create" or CraftingTypes.GatherPermission or CraftingTypes.BlacksmithPermission)).ToList();
            if (grantPotions) actions.Add("items.potion.create");
            if (grantCooking) actions.Add("items.cooking.create");
            if (grantGathering) actions.Add(CraftingTypes.GatherPermission);
            if (grantBlacksmithing) actions.Add(CraftingTypes.BlacksmithPermission);
            SaveMemberPermissions(target, new MemberCapabilities(actions.ToArray(), tradingAllowed, combatAllowed));
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
        Mutate("PUT", $"api/v2/seasons/{community!.Id}/members/{target.Character.Id}/capabilities", permissions, memberDraftEtag ?? target.Etag);
    }
    private void DrawCommunityPermissions(Member target)
    {
        if (!ImGui.CollapsingHeader("Season permissions")) return;
        DrawPermissionConflict(target);
        ImGui.TextUnformatted("Moderation");
        ImGui.Checkbox("General Moderation", ref grantGeneralModeration);
        ImGui.Checkbox("Manage Gameplay Permissions", ref grantGameplayPermissions);
        ImGui.Checkbox("Create Announcements", ref grantAnnouncements);
        ImGui.Checkbox("Manage Slave listings", ref grantPermitListings);

        ImGui.Separator(); ImGui.TextUnformatted("Recipe creation");
        ImGui.Checkbox("Create Alchemy Recipes", ref grantDefinitions);
        ImGui.Checkbox("Create Cooking Recipes", ref grantCookingDefinitions);
        ImGui.Checkbox("Create Blacksmithing Recipes", ref grantBlacksmithDefinitions);
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
            var actions = target.Capabilities.Where(x => x is not ("currency.manage" or "inventory.remove" or "items.potion.define" or "season.moderate" or "announcements.manage" or "members.gameplay.manage" or "groups.create" or "groups.manage" or "items.cooking.define" or CraftingTypes.BlacksmithRecipePermission or ManagePermitListings)).ToList();
            if (grantPermitListings) actions.Add(ManagePermitListings);
            if (grantGroupCreation) actions.Add("groups.create");
            if (grantGroupManagement) actions.Add("groups.manage");
            if (grantGeneralModeration) actions.Add("season.moderate");
            if (grantGameplayPermissions) actions.Add("members.gameplay.manage");
            if (grantAnnouncements) actions.Add("announcements.manage");
            if (grantCoins) actions.Add("currency.manage");
            if (grantRemoval) actions.Add("inventory.remove");
            if (grantDefinitions) actions.Add("items.potion.define");
            if (grantCookingDefinitions) actions.Add("items.cooking.define");
            if (grantBlacksmithDefinitions) actions.Add(CraftingTypes.BlacksmithRecipePermission);
            SaveMemberPermissions(target, new MemberCapabilities(actions.ToArray()));
        }
        ImGui.EndDisabled();
    }
    private void DrawMemberOwnershipTransfer(Member target)
    {
        if (community != null && ShiftMemberButton("Propose ownership transfer"))
            Mutate("POST", $"api/v2/seasons/{community.Id}/ownership-transfers", new OwnershipProposal(target.Character.Id));
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
