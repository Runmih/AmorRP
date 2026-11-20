using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private enum CommunityBrowserPage { Overview, Members, Groups, Noticeboard }
    private CommunityBrowserPage? requestedCommunityPage;
    private bool requestedManagementMembers;
    public Action<WorkspaceKind>? OpenWorkspaceWindow { get; set; }
    public bool CanOpenGroupManagement => WorkspaceAccess.OpenGroupManagement(community, session?.Character.Id, communityGroups);
    public bool CanOpenCommunityManagement => CanCreateSeasons || IsSeasonAdministrator || WorkspaceAccess.CommunityManagement(community, session?.Character.Id);
    private bool CanManageCommunityMembers => Owner || CanManageGameplay || CanCoins || community?.MyCapabilities.Contains("inventory.remove") == true;

    public void OpenWorkspace(WorkspaceKind kind) => OpenWorkspaceWindow?.Invoke(kind);

    private void DrawWorkspace(WorkspaceKind kind)
    {
        if (kind == WorkspaceKind.Inventory)
        {
            DrawInventoryWorkspace();
            return;
        }

        ImGui.TextColored(InventoryGold, WorkspaceWindow.Title(kind).ToUpperInvariant());
        DrawCommunitySelection();
        if (community == null)
        { if (kind == WorkspaceKind.CommunityManagement && (CanCreateSeasons || IsSeasonAdministrator)) DrawSeasonManagement(); else DrawNoSeason(); return; }
        ImGui.BeginChild("WorkspaceContent", System.Numerics.Vector2.Zero, false);
        switch (kind)
        {
            case WorkspaceKind.Crafting: DrawCraftingTabs(includeWriting: false); break;
            case WorkspaceKind.GroupManagement:
                if (CanOpenGroupManagement) DrawCommunityGroups(GroupView.Management);
                else Wrapped("You can manage groups you own or have staff permissions for. Group creation requires season permission.");
                break;
            case WorkspaceKind.CommunityManagement:
                if (CanOpenCommunityManagement) DrawCommunityManagementWorkspace();
                else Wrapped("Season management requires an owner or a relevant delegated permission.");
                break;
            case WorkspaceKind.Season: DrawCommunityBrowser(); break;
        }
        ImGui.EndChild(); DrawSharedDialogsOnce();
    }

    private void DrawCommunityBrowser()
    {
        if (!ImGui.BeginTabBar("CommunityBrowserTabs", ImGuiTabBarFlags.FittingPolicyScroll)) return;
        foreach (var page in Enum.GetValues<CommunityBrowserPage>())
        {
            var flags = requestedCommunityPage == page ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
            if (!ImGui.BeginTabItem(page.ToString(), flags)) continue;
            if (requestedCommunityPage == page) requestedCommunityPage = null;
            switch (page)
            {
                case CommunityBrowserPage.Overview:
                    DrawHomeOverview(editable: false); DrawMyBills(); DrawOwnershipOffer(); break;
                case CommunityBrowserPage.Members: DrawMembers(MemberView.Directory); break;
                case CommunityBrowserPage.Groups: DrawCommunityGroups(GroupView.Browse); break;
                case CommunityBrowserPage.Noticeboard: DrawNoticeboard(communityBrowser: true); break;
            }
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawCommunityManagementWorkspace()
    {
        if (!ImGui.BeginTabBar("CommunityManagementTabs", ImGuiTabBarFlags.FittingPolicyScroll)) return;
        if ((CanCreateSeasons || IsSeasonAdministrator) && ImGui.BeginTabItem("Seasons")) { DrawSeasonManagement(); ImGui.EndTabItem(); }
        if (CanGeneralModerate && ImGui.BeginTabItem("Profile")) { DrawHomeProfile(); DrawOwnershipCancellation(); ImGui.EndTabItem(); }
        var membersFlags = requestedManagementMembers ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (CanManageCommunityMembers && ImGui.BeginTabItem("Members", membersFlags))
        { requestedManagementMembers = false; DrawMembers(MemberView.Management); ImGui.EndTabItem(); }
        if (CanAnnounce && ImGui.BeginTabItem("Announcements")) { DrawAnnouncements(); ImGui.EndTabItem(); }
        if (CanManageGroups && ImGui.BeginTabItem(Owner ? "Group access" : "Groups"))
        { DrawCommunityGroups(GroupView.CommunityAdministration); ImGui.EndTabItem(); }
        if (CanGeneralModerate && ImGui.BeginTabItem("Currency"))
        { LoadHomeDrafts(); DrawCurrencyNameEditor(); if (Owner) DrawCurrencyIconEditor(); ImGui.EndTabItem(); }
        if (Owner && ImGui.BeginTabItem("Weekly limits")) { DrawWeeklyPolicy(expanded: true); ImGui.EndTabItem(); }
        if (Owner && ImGui.BeginTabItem("Economy")) { DrawBillingPolicy(); ImGui.EndTabItem(); }
        if (IsSeasonAdministrator && ImGui.BeginTabItem("Creation access")) { DrawSeasonCreationAccess(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
}
