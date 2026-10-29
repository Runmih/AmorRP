using System.Globalization;
using System.Numerics;
using AmorRP.Contracts.Groups;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private GroupHub hub = new([], []);
    private GroupPrivate? groupPrivate;
    private Guid? privateRequestedFor, eventEditId, announcementOpen;
    private int eventEditVersion;
    private string groupJoinPassword = "", noticeSearch = "";
    private string noticeTitle = "", noticeMessage = "", noticeReward = "";
    private Guid? noticeDraftFor;
    private int noticeDraftVersion;
    private string eventTitle = "", eventDescription = "", eventLocation = "", eventStart = "", eventEnd = "";
    private string groupAnnouncementTitle = "", groupAnnouncementMessage = "";
    private string GroupPath(AmorRP.Contracts.Groups.Group group) => $"api/v2/communities/{group.CommunityId}/groups/{group.Id}";
    private void LoadGroupPrivate(AmorRP.Contracts.Groups.Group group, bool force = false, bool more = false)
    {
        if (!group.IsMember || (!force && privateRequestedFor == group.Id)) return;
        var previous = more ? groupPrivate : null;
        privateRequestedFor = group.Id; if (!more) groupPrivate = null;
        var id = group.Id; var communityId = group.CommunityId; var token = session!.AccessToken; var uri = origin!;
        Start(async ct => {
            var summary = await client.GetAsync<AmorRP.Contracts.Groups.Group>(uri, GroupPath(group), token, ct);
            var content = await client.GetAsync<GroupPrivate>(uri, GroupPath(group) + "/private" + (previous?.NextCursor == null ? "" : "?cursor=" + Uri.EscapeDataString(previous.NextCursor)), token, ct);
            return () => {
                if (selectedGroupId != id || community?.Id != communityId || !summary.IsMember) return;
                communityGroups = communityGroups.Select(x => x.Id == id ? summary : x).ToArray();
                groupPrivate = previous == null ? content : content with {
                    Members = previous.Members.Concat(content.Members).DistinctBy(x => x.CharacterId).ToArray()
                };
            };
        }, "Loading private group contents…");
    }
    private void DrawGroupSections(AmorRP.Contracts.Groups.Group group)
    {
        if (!ImGui.BeginTabBar("GroupSections")) return;
        if (ImGui.BeginTabItem("Members")) { DrawGroupMembers(group); ImGui.EndTabItem(); }
        if (group.IsMember && ImGui.BeginTabItem("Announcements"))
        {
            LoadGroupPrivate(group);
            if (ActionButton("Refresh announcements", mutation: false)) LoadGroupPrivate(group, true);
            if (CanGroupAction(group, GroupCapabilities.Announcements))
            {
                FieldLabel("Title"); LimitedInput("##GroupAnnouncementTitle", ref groupAnnouncementTitle, AmorRP.Contracts.Announcements.AnnouncementCreate.MaximumTitleCharacters);
                FieldLabel("Message"); WrappedInput("##GroupAnnouncementMessage", ref groupAnnouncementMessage, 524288, new(-1, ImGui.GetTextLineHeightWithSpacing() * 4), maximumCharacters: AmorRP.Contracts.Announcements.AnnouncementCreate.MaximumBodyCharacters);
                ImGui.BeginDisabled(string.IsNullOrWhiteSpace(groupAnnouncementTitle) || string.IsNullOrWhiteSpace(groupAnnouncementMessage));
                if (ActionButton("Publish announcement")) Mutate("POST", GroupPath(group) + "/announcements", new AnnouncementWrite(groupAnnouncementTitle, groupAnnouncementMessage));
                ImGui.EndDisabled();
            }
            if (groupPrivate != null) foreach (var item in groupPrivate.Announcements)
            {
                if (ImGui.Selectable(item.Title + "###ga" + item.Id, announcementOpen == item.Id)) announcementOpen = announcementOpen == item.Id ? null : item.Id;
                if (announcementOpen == item.Id) Wrapped(item.Message);
            }
            if (groupPrivate?.Announcements.Length == 0) Wrapped("No group announcements yet.");
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Events")) { DrawGroupEvents(group); ImGui.EndTabItem(); }
        if (CanGroupAction(group, GroupCapabilities.Notice) && ImGui.BeginTabItem("Notice")) { DrawNoticeEditor(group); ImGui.EndTabItem(); }
        if (OwnSelectedGroup && ImGui.BeginTabItem("Staff")) { DrawGroupStaff(group); ImGui.EndTabItem(); }
        if (Owner && ImGui.BeginTabItem("Features")) { DrawGroupFeatures(group); ImGui.EndTabItem(); }
        ImGui.EndTabBar();
    }
    private void DrawGroupMembers(AmorRP.Contracts.Groups.Group group)
    {
        Wrapped("Joining: " + (group.JoinMode == "password" ? "Password protected" : group.JoinMode));
        if (!group.IsMember)
        {
            ImGui.BeginDisabled(group.JoinMode == "closed");
            if (ActionButton("Join group"))
            {
                if (group.JoinMode == "password") { groupJoinPassword = ""; ImGui.OpenPopup("Group password"); }
                else Mutate("POST", GroupPath(group) + "/join", new GroupJoin());
            }
            ImGui.EndDisabled();
            if (ImGui.BeginPopupModal("Group password", ImGuiWindowFlags.AlwaysAutoResize))
            {
                Wrapped("Password for " + group.Name);
                ImGui.InputText("##JoinGroupPassword", ref groupJoinPassword, 65, ImGuiInputTextFlags.Password);
                ImGui.BeginDisabled(groupJoinPassword.Length == 0);
                if (ActionButton("Join")) { Mutate("POST", GroupPath(group) + "/join", new GroupJoin(groupJoinPassword)); groupJoinPassword = ""; ImGui.CloseCurrentPopup(); }
                ImGui.EndDisabled(); ImGui.SameLine();
                if (ImGui.Button("Cancel")) { groupJoinPassword = ""; ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }
            Wrapped("Join to view this group's member list and announcements."); return;
        }
        LoadGroupPrivate(group);
        if (ActionButton("Refresh group members", mutation: false)) LoadGroupPrivate(group, true);
        if (groupPrivate != null) foreach (var member in groupPrivate.Members)
            Wrapped(member.Name + " @ " + member.World + (member.IsOwner ? " · owner" : ""));
        if (groupPrivate?.NextCursor != null && ActionButton("More group members", mutation: false)) LoadGroupPrivate(group, true, true);
        if (!OwnSelectedGroup && ActionButton("Leave group")) Mutate("DELETE", GroupPath(group) + "/membership", null);
    }
    private void DrawNoticeboard()
    {
        if (community == null) { Wrapped("Choose a community to read its noticeboard."); return; }
        DrawRefreshState(hubRefresh, "Noticeboard");
        Wrapped("Community requests and offered rewards · one notice per group");
        LimitedInput("Filter notices", ref noticeSearch, 100);
        foreach (var item in hub.Notices.Where(x => (x.Title + " " + x.Message + " " + x.GroupName + " " + x.Reward).Contains(noticeSearch, StringComparison.OrdinalIgnoreCase)))
        {
            ImGui.PushID(item.GroupId.ToString()); ImGui.Separator();
            Wrapped(item.GroupName); Wrapped(item.Title); Wrapped(item.Message);
            if (item.Reward.Length > 0) Wrapped("Offered reward: " + item.Reward);
            if (ImGui.SmallButton("View group") && communityGroups.FirstOrDefault(x => x.Id == item.GroupId) is { } group) { SelectGroup(group); mainSection = MainSection.Groups; }
            ImGui.PopID();
        }
        if (hub.Notices.Length == 0) Wrapped("No requests posted yet. Group owners and authorized staff can publish one from their group's Notice tab.");
    }
    private void DrawNoticeEditor(AmorRP.Contracts.Groups.Group group)
    {
        DrawRefreshState(hubRefresh, "Notice");
        if (!hubRefresh.HasLoaded) return;
        var current = hub.Notices.FirstOrDefault(x => x.GroupId == group.Id);
        if (noticeDraftFor != group.Id)
        { noticeDraftFor = group.Id; noticeTitle = current?.Title ?? ""; noticeMessage = current?.Message ?? ""; noticeReward = current?.Reward ?? ""; noticeDraftVersion = current?.Version ?? 0; }
        FieldLabel("Request title"); LimitedInput("##NoticeTitle", ref noticeTitle, 40);
        FieldLabel("Request"); WrappedInput("##NoticeMessage", ref noticeMessage, 8192, new(-1, ImGui.GetTextLineHeightWithSpacing() * 4), maximumCharacters: 300);
        FieldLabel("Offered reward (optional)"); LimitedInput("##NoticeReward", ref noticeReward, 100);
        Wrapped("Rewards are agreed between players; publishing does not move currency or reserve items.");
        var stale = noticeDraftVersion != (current?.Version ?? 0);
        if (stale) { Wrapped("This notice changed. Reload before saving."); if (ImGui.Button("Reload notice")) noticeDraftFor = null; }
        ImGui.BeginDisabled(stale || string.IsNullOrWhiteSpace(noticeTitle) || string.IsNullOrWhiteSpace(noticeMessage));
        if (ActionButton(current == null ? "Publish notice" : "Save notice")) Mutate("PUT", GroupPath(group) + "/notice", new NoticeWrite(noticeTitle, noticeMessage, noticeReward), current == null ? null : ETag("group-notice", group.Id, noticeDraftVersion));
        ImGui.EndDisabled();
        if (current != null && ActionButton("Remove notice")) Mutate("DELETE", GroupPath(group) + "/notice", null, ETag("group-notice", group.Id, current.Version));
    }
    private static bool EventTime(string text, out DateTimeOffset value) => DateTimeOffset.TryParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    private void NewGroupEvent()
    { eventEditId = null; eventEditVersion = 0; eventTitle = eventDescription = eventLocation = ""; eventStart = DateTimeOffset.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm"); eventEnd = DateTimeOffset.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm"); }
    private void DrawGroupEvents(AmorRP.Contracts.Groups.Group group)
    {
        DrawRefreshState(hubRefresh, "Group events");
        foreach (var item in hub.Events.Where(x => x.GroupId == group.Id))
        {
            ImGui.PushID(item.Id.ToString()); DrawEvent(item);
            if (CanGroupAction(group, GroupCapabilities.Events))
            {
                if (ImGui.SmallButton("Edit")) { eventEditId = item.Id; eventEditVersion = item.Version; eventTitle = item.Title; eventDescription = item.Description; eventLocation = item.Location; eventStart = item.StartsAt.ToString("yyyy-MM-dd HH:mm"); eventEnd = item.EndsAt.ToString("yyyy-MM-dd HH:mm"); }
                ImGui.SameLine(); if (ActionButton("Cancel event")) Mutate("DELETE", GroupPath(group) + "/events/" + item.Id, null, ETag("group-event", item.Id, item.Version));
            }
            ImGui.PopID();
        }
        if (!CanGroupAction(group, GroupCapabilities.Events)) return;
        if (ImGui.Button("New event")) NewGroupEvent();
        if (eventStart.Length == 0) NewGroupEvent();
        ImGui.Separator(); FieldLabel("Event title"); LimitedInput("##EventTitle", ref eventTitle, 40);
        FieldLabel("Description"); WrappedInput("##EventDescription", ref eventDescription, 8192, new(-1, ImGui.GetTextLineHeightWithSpacing() * 3), maximumCharacters: 100);
        FieldLabel("Location"); LimitedInput("##EventLocation", ref eventLocation, 100);
        FieldLabel("Starts · Server Time / UTC · YYYY-MM-DD HH:MM"); LimitedInput("##EventStart", ref eventStart, 16);
        FieldLabel("Ends · Server Time / UTC · YYYY-MM-DD HH:MM"); LimitedInput("##EventEnd", ref eventEnd, 16);
        var valid = EventTime(eventStart, out var start) && EventTime(eventEnd, out var end) && end > start && end > DateTimeOffset.UtcNow;
        // Parse independently so both values are initialized even after short-circuit validation.
        EventTime(eventEnd, out var finish);
        if (!valid) Wrapped("Enter a valid date and time range, with an end after the start.");
        ImGui.BeginDisabled(!valid || string.IsNullOrWhiteSpace(eventTitle) || string.IsNullOrWhiteSpace(eventDescription) || string.IsNullOrWhiteSpace(eventLocation));
        if (ActionButton(eventEditId == null ? "Schedule event" : "Save event")) Mutate(eventEditId == null ? "POST" : "PUT", GroupPath(group) + "/events" + (eventEditId == null ? "" : "/" + eventEditId), new EventWrite(eventTitle, eventDescription, eventLocation, start, finish), eventEditId.HasValue ? ETag("group-event", eventEditId.Value, eventEditVersion) : null);
        ImGui.EndDisabled();
    }
    private static void DrawEvent(GroupEvent item)
    {
        ImGui.Separator(); Wrapped(item.GroupName + " · " + item.Title);
        Wrapped($"{item.StartsAt:yyyy-MM-dd HH:mm} → {item.EndsAt:yyyy-MM-dd HH:mm} ST / UTC");
        Wrapped(item.Location); if (item.Description.Length > 0) Wrapped(item.Description);
    }
}
