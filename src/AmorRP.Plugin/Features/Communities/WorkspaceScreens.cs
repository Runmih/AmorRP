using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private enum CommunityBrowserPage { Overview, Members, Groups, Noticeboard }
    private CommunityBrowserPage? requestedCommunityPage;
    private bool requestedManagementMembers;
    public Action<WorkspaceKind>? OpenWorkspaceWindow { get; set; }
    public bool CanOpenGroupManagement => WorkspaceAccess.OpenGroupManagement(community, session?.Character.Id, communityGroups);
    public bool CanOpenCommunityManagement => WorkspaceAccess.CommunityManagement(community, session?.Character.Id);
    private bool CanManageCommunityMembers => Owner || CanManageGameplay || CanCoins || community?.MyCapabilities.Contains("inventory.remove") == true;

    public void OpenWorkspace(WorkspaceKind kind) => OpenWorkspaceWindow?.Invoke(kind);

    private void DrawWorkspace(WorkspaceKind kind, PendingCommand? pending)
    {
        ImGui.TextColored(InventoryGold, WorkspaceWindow.Title(kind).ToUpperInvariant());
        DrawCommunitySelection();
        if (community == null)
        { Wrapped("Choose a community above to get started."); DrawActionFeedback(pending); return; }
        var feedbackHeight = ActionFeedbackHeight(pending);
        ImGui.BeginChild("WorkspaceContent", new(0, feedbackHeight > 0 ? -feedbackHeight - ImGui.GetStyle().ItemSpacing.Y : 0), false);
        switch (kind)
        {
            case WorkspaceKind.Crafting: DrawCraftingTabs(includeWriting: false); break;
            case WorkspaceKind.GroupManagement:
                if (CanOpenGroupManagement) DrawCommunityGroups(GroupView.Management);
                else Wrapped("You can manage groups you own or have staff permissions for. Group creation requires community permission.");
                break;
            case WorkspaceKind.CommunityManagement:
                if (CanOpenCommunityManagement) DrawCommunityManagementWorkspace();
                else Wrapped("Community management requires an owner or a relevant delegated permission.");
                break;
            case WorkspaceKind.Community: DrawCommunityBrowser(); break;
        }
        ImGui.EndChild(); DrawActionFeedback(pending); DrawSharedDialogsOnce();
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
        if (CanGeneralModerate && ImGui.BeginTabItem("Profile")) { DrawHomeProfile(); ImGui.EndTabItem(); }
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
        if (Owner && ImGui.BeginTabItem("Invitations")) { DrawInvitations(); ImGui.EndTabItem(); }
        if (Owner && ImGui.BeginTabItem("Ownership & deletion")) { DrawCommunityLifecycle(); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
}
