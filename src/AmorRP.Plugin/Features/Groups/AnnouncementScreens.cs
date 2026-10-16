using System.Numerics;
using System.Globalization;
using System.Text.Json;
using AmorRP.Contracts.Announcements;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private readonly Dictionary<Guid, int> announcementUnread = [];
    private AnnouncementSummary[] announcements = [];
    private Announcement? openAnnouncement;
    private Task<Action>? announcementWork;
    private DateTimeOffset nextAnnouncementPoll;
    private long announcementEpoch;
    private bool announcementsLoaded, openAnnouncementReader;
    private string announcementTitle = "", announcementBody = "", announcementError = "", announcementPostError = "";
    public int UnreadAnnouncements => session == null ? 0 : announcementUnread.Where(x => groups.Any(g => g.Id == x.Key)).Sum(x => x.Value);
    private void DrawAnnouncementBadge() => AmorRP.Plugin.Features.Shell.NotificationBadge.Draw();
    public void FocusUnreadAnnouncements()
    {
        if (UnreadAnnouncements == 0) return;
        mainSection = MainSection.Home;
        var first = groups.OrderByDescending(g => g.Id == group?.Id).FirstOrDefault(g => announcementUnread.GetValueOrDefault(g.Id) > 0);
        if (first != null && first.Id != group?.Id && ActiveTrade == null && !WriteBlocked) SwitchGroup(first);
    }
    private void InvalidateAnnouncements() { announcementEpoch++; nextAnnouncementPoll = default; }
    private void ClearSelectedAnnouncements()
    {
        announcements = []; announcementsLoaded = false; openAnnouncement = null;
        openAnnouncementReader = false;
        announcementTitle = announcementBody = announcementError = announcementPostError = ""; InvalidateAnnouncements();
    }
    private void ClearAnnouncementNotifications()
    {
        announcementUnread.Clear(); ClearSelectedAnnouncements();
        if (announcementWork != null) _ = announcementWork.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
        announcementWork = null;
    }
    private void TickAnnouncements()
    {
        if (announcementWork is { IsCompleted: true })
        {
            var done = announcementWork; announcementWork = null;
            if (done.IsCompletedSuccessfully) done.Result();
            else { _ = done.Exception; if (done.IsCanceled) nextAnnouncementPoll = default; else announcementError = "Announcements could not refresh. Retrying shortly."; }
        }
        if (session == null || groups.Length == 0) { announcementUnread.Clear(); return; }
        if (announcementWork != null || session == null || origin == null || !serviceCompatible || currentCommand != null
            || groups.Length == 0 || session.AccessExpiresAt <= DateTimeOffset.UtcNow || DateTimeOffset.UtcNow < nextAnnouncementPoll) return;
        nextAnnouncementPoll = DateTimeOffset.UtcNow.AddSeconds(30);
        var uri = origin; var token = session.AccessToken; var actor = session.Character.Id; var selectedId = group?.Id;
        var epoch = announcementEpoch; var ct = lifetime.Token;
        announcementWork = Task.Run(async () =>
        {
            var unread = await client.GetAsync<AnnouncementUnreadPage>(uri, "api/v1/announcements/unread", token, ct);
            AnnouncementPage? page = null;
            if (selectedId.HasValue && unread.Groups.Any(x => x.GroupId == selectedId))
                page = await client.GetAsync<AnnouncementPage>(uri, $"api/v1/groups/{selectedId}/announcements", token, ct);
            return (Action)(() =>
            {
                if (epoch != announcementEpoch || actor != session?.Character.Id || uri != origin || selectedId != group?.Id)
                { nextAnnouncementPoll = default; return; }
                announcementUnread.Clear(); foreach (var entry in unread.Groups) announcementUnread[entry.GroupId] = entry.UnreadCount;
                announcements = page?.Items ?? [];
                if (selectedId.HasValue && page != null) announcementUnread[selectedId.Value] = page.Items.Count(x => x.Unread);
                announcementsLoaded = true; announcementError = "";
            });
        }, ct);
    }
    private void DrawAnnouncements()
    {
        ImGui.TextUnformatted("Announcements"); ImGui.Separator();
        if (CanAnnounce) DrawAnnouncementComposer();
        if (announcementError.Length > 0) Wrapped(announcementError);
        else if (!announcementsLoaded) ImGui.TextDisabled("Loading announcements…");
        else if (announcements.Length == 0) ImGui.TextDisabled("No announcements yet.");
        foreach (var post in announcements)
        {
            ImGui.PushID(post.Id.ToString());
            var position = ImGui.GetCursorScreenPos();
            if (ImGui.Selectable("##AnnouncementTitle", false, ImGuiSelectableFlags.None, new(0, ImGui.GetTextLineHeightWithSpacing() * 2))) OpenAnnouncement(post.Id);
            if (post.Unread) DrawAnnouncementBadge();
            var title = post.Title;
            var available = Math.Max(30, ImGui.GetContentRegionAvail().X - 18);
            if (ImGui.CalcTextSize(title).X > available)
            {
                var boundaries = StringInfo.ParseCombiningCharacters(title); var count = boundaries.Length;
                while (count > 1 && ImGui.CalcTextSize(title[..boundaries[count - 1]] + "…").X > available) count--;
                title = title[..boundaries[Math.Max(0, count - 1)]] + "…";
            }
            ImGui.GetWindowDrawList().AddText(position, ImGui.GetColorU32(ImGuiCol.Text), title);
            ImGui.GetWindowDrawList().AddText(position + new Vector2(0, ImGui.GetTextLineHeightWithSpacing()), ImGui.GetColorU32(ImGuiCol.TextDisabled), post.PublishedAt.LocalDateTime.ToString("d"));
            ImGui.PopID();
        }
    }
    private void OpenAnnouncement(Guid id)
    {
        var groupId = group!.Id; var uri = origin!; var token = session!.AccessToken;
        Start(async ct =>
        {
            var path = $"api/v1/groups/{groupId}/announcements/{id}";
            var post = await client.GetAsync<Announcement>(uri, path, token, ct);
            var json = await client.CommandAsync(uri, new CommandRequest("PUT", path + "/read", null, null), Guid.CreateVersion7(), token, ct);
            var read = JsonSerializer.Deserialize<AnnouncementReadResult>(json, FoundationClient.Json)!;
            return () =>
            {
                if (group?.Id != groupId) return;
                openAnnouncement = post; openAnnouncementReader = true;
                announcementUnread[groupId] = read.UnreadCount;
                announcements = announcements.Select(x => x.Id == id ? x with { Unread = false } : x).ToArray();
                InvalidateAnnouncements();
            };
        }, "Opening announcement…");
    }
    private void DrawAnnouncementComposer()
    {
        var previousTitle = announcementTitle; var previousBody = announcementBody;
        ImGui.PushItemWidth(-1);
        FieldLabel("Title"); LimitedInput("##AnnouncementTitle", ref announcementTitle, AnnouncementCreate.MaximumTitleCharacters);
        FieldLabel("Message"); WrappedInput("##AnnouncementMessage", ref announcementBody, 65536, new(-1, ImGui.GetTextLineHeightWithSpacing() * 5), maximumCharacters: AnnouncementCreate.MaximumBodyCharacters);
        if (previousTitle != announcementTitle || previousBody != announcementBody) announcementPostError = "";
        var error = PlainText.Error(announcementTitle, AnnouncementCreate.MaximumTitleCharacters)
            ?? PlainText.Error(announcementBody, AnnouncementCreate.MaximumBodyCharacters, multiline: true);
        if (error != null && (announcementTitle.Length > 0 || announcementBody.Length > 0)) Wrapped(error);
        if (announcementPostError.Length > 0) Wrapped(announcementPostError);
        ImGui.BeginDisabled(error != null);
        if (ActionButton("Publish")) { announcementPostError = ""; Mutate("POST", $"api/v1/groups/{group!.Id}/announcements", new AnnouncementCreate(announcementTitle, announcementBody)); }
        ImGui.EndDisabled(); ImGui.PopItemWidth(); ImGui.Spacing(); ImGui.Separator();
    }
    private void DrawAnnouncementDialogs()
    {
        if (openAnnouncementReader) { ImGui.OpenPopup("Announcement###ReadAnnouncement"); openAnnouncementReader = false; }
        ImGui.SetNextWindowSize(new Vector2(600, 450), ImGuiCond.FirstUseEver);
        if (ImGui.BeginPopupModal("Announcement###ReadAnnouncement", ImGuiWindowFlags.NoCollapse))
        {
            if (openAnnouncement == null) ImGui.CloseCurrentPopup();
            else
            {
                Wrapped(openAnnouncement.Title);
                ImGui.TextDisabled(openAnnouncement.Author.DisplayName + " @ " + openAnnouncement.Author.HomeWorldName + " · " + openAnnouncement.PublishedAt.LocalDateTime.ToString("g"));
                ImGui.BeginChild("AnnouncementBody", new(0, -ImGui.GetFrameHeightWithSpacing()), true);
                Wrapped(openAnnouncement.Body); ImGui.EndChild();
                if (ImGui.Button("Close")) ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
