using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Announcements;
using AmorRP.Plugin.Services.Api;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private bool homeDraftLoaded, homeDescriptionExpanded;
    private string editGroupName = "", savedGroupName = "", savedHomeDescription = "", savedCurrencyName = "";
    private void LoadHomeDrafts()
    {
        var current = group!;
        if (!homeDraftLoaded || editGroupName == savedGroupName) editGroupName = savedGroupName = current.Name;
        if (!homeDraftLoaded || editDescription == savedHomeDescription) editDescription = savedHomeDescription = current.Description;
        if (savedGroupName == current.Name && savedHomeDescription == current.Description) editGroupVersion = current.Version;
        if (!homeDraftLoaded || editCurrencyName == savedCurrencyName)
        { editCurrencyName = savedCurrencyName = current.Currency.Name; editCurrencyVersion = current.Currency.Version; }
        else if (savedCurrencyName == current.Currency.Name) editCurrencyVersion = current.Currency.Version;
        homeDraftLoaded = true;
    }
    private void DrawHomeProfile()
    {
        LoadHomeDrafts();
        var current = group!;
        var spacing = ImGui.GetTextLineHeightWithSpacing();
        var textWidth = Math.Max(100, ImGui.GetContentRegionAvail().X - 150);
        var descriptionHeight = ImGui.CalcTextSize(current.Description, false, textWidth).Y;
        var shownHeight = homeDescriptionExpanded ? descriptionHeight : Math.Min(descriptionHeight, spacing * 3);
        var height = CanGeneralModerate ? spacing * 4 + ImGui.GetFrameHeightWithSpacing() * 3 + 28 : Math.Max(128, shownHeight + spacing * 3 + 20);
        BeginHomeCard("GroupProfile", height);
        if (ImGui.BeginTable("ProfileLayout", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Logo", ImGuiTableColumnFlags.WidthFixed, 116);
            ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn(); DrawGroupLogo(); ImGui.TableNextColumn(); ImGui.PushItemWidth(-1);
            if (Owner) LimitedInput("##HomeGroupName", ref editGroupName, 80, countCodeUnits: true);
            else ImGui.TextUnformatted(current.Name);
            if (CanGeneralModerate)
            {
                WrappedInput("##HomeDescription", ref editDescription, 8192, new(-1, spacing * 4), maximumCharacters: GroupEdit.MaximumDescriptionCharacters);
                var conflict = savedGroupName != current.Name || savedHomeDescription != current.Description;
                var error = string.IsNullOrWhiteSpace(editGroupName) || editGroupName.Length > 80 ? "Group name must contain 1–80 characters."
                    : AmorRP.Core.Items.PlainText.Characters(editDescription) > GroupEdit.MaximumDescriptionCharacters ? "Description must be at most 300 characters." : null;
                if (error != null) Wrapped(error);
                if (conflict) { Wrapped("The saved profile changed elsewhere."); if (ImGui.Button("Reload profile")) { homeDraftLoaded = false; LoadHomeDrafts(); } }
                var dirty = editGroupName != current.Name || editDescription != current.Description;
                if (dirty)
                {
                    ImGui.BeginDisabled(conflict || error != null);
                    if (ActionButton("Save profile")) Mutate("PATCH", $"api/v1/groups/{current.Id}", new GroupEdit(Owner ? editGroupName : current.Name, editDescription), ETag("group", current.Id, editGroupVersion));
                    ImGui.EndDisabled();
                }
            }
            else
            {
                ImGui.BeginChild("GroupDescription", new(0, Math.Max(spacing, shownHeight)), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                Wrapped(current.Description.Length == 0 ? "No group description yet." : current.Description); ImGui.EndChild();
                if (descriptionHeight > spacing * 3 && ImGui.SmallButton(homeDescriptionExpanded ? "Show less" : "Read more")) homeDescriptionExpanded = !homeDescriptionExpanded;
            }
            ImGui.PopItemWidth(); ImGui.EndTable();
        }
        EndHomeCard();
    }
    private void DrawHomeResources()
    {
        ImGui.TextUnformatted("Your resources"); ImGui.Separator();
        if (icons.HasImage) { icons.DrawIcon(); ImGui.SameLine(); }
        Wrapped(group!.MyBalance.Available + " " + group.Currency.Name);
        if (group.MyBalance.Reserved != "0") ImGui.TextDisabled(group.MyBalance.Reserved + " reserved for trading");
        if (CanGeneralModerate)
        {
            FieldLabel("Currency name"); LimitedInput("##HomeCurrencyName", ref editCurrencyName, 80, countCodeUnits: true);
            var conflict = savedCurrencyName != group.Currency.Name;
            if (conflict) { Wrapped("Currency name changed elsewhere."); if (ImGui.Button("Reload currency name")) { editCurrencyName = savedCurrencyName = group.Currency.Name; editCurrencyVersion = group.Currency.Version; } }
            if (editCurrencyName != group.Currency.Name)
            {
                ImGui.BeginDisabled(conflict || string.IsNullOrWhiteSpace(editCurrencyName) || editCurrencyName.Length > 80);
                if (ActionButton("Save currency name")) Mutate("PATCH", $"api/v1/groups/{group.Id}/currency", new CurrencyEdit(editCurrencyName, group.Currency.Symbol), ETag("currency", group.Currency.Id, editCurrencyVersion));
                ImGui.EndDisabled();
            }
        }
        ImGui.Spacing(); ImGui.Separator();
        if (CanCraftPotions) DrawQuota("potion_points");
        DrawQuota("letters");
    }
    private void ApplyHomeSuccess(CommandRequest request, string json)
    {
        if (request.Method == "POST" && request.Path.EndsWith("/announcements", StringComparison.Ordinal))
        {
            var submitted = JsonSerializer.Deserialize<AnnouncementCreate>(request.Json!, FoundationClient.Json)!;
            if (announcementTitle == submitted.Title && announcementBody == submitted.Body) announcementTitle = announcementBody = "";
            announcementPostError = ""; InvalidateAnnouncements(); return;
        }
        if (group == null) return;
        if (request.Path == $"api/v1/groups/{group.Id}/logo")
        {
            var updated = JsonSerializer.Deserialize<CommandResult<Group>>(json, FoundationClient.Json)!.Result;
            group = group with { LogoAssetId = updated.LogoAssetId, LogoVersion = updated.LogoVersion };
            logos.Clear(); return;
        }
        if (request.Method == "PATCH" && request.Path == $"api/v1/groups/{group.Id}")
        {
            var updated = JsonSerializer.Deserialize<CommandResult<Group>>(json, FoundationClient.Json)!.Result;
            var submitted = JsonSerializer.Deserialize<GroupEdit>(request.Json!, FoundationClient.Json)!;
            group = updated; groups = groups.Select(x => x.Id == updated.Id ? updated : x).ToArray();
            if (editGroupName == submitted.Name) editGroupName = updated.Name;
            if (editDescription == submitted.Description) editDescription = updated.Description;
            savedGroupName = updated.Name; savedHomeDescription = updated.Description; editGroupVersion = updated.Version;
        }
        if (request.Method == "PATCH" && request.Path == $"api/v1/groups/{group.Id}/currency")
        {
            var updated = JsonSerializer.Deserialize<CommandResult<AmorRP.Contracts.Currency.Currency>>(json, FoundationClient.Json)!.Result;
            var submitted = JsonSerializer.Deserialize<CurrencyEdit>(request.Json!, FoundationClient.Json)!;
            group = group with { Currency = updated };
            if (editCurrencyName == submitted.Name) editCurrencyName = updated.Name;
            savedCurrencyName = updated.Name; editCurrencyVersion = updated.Version;
        }
    }
    private void ClearHomeViews()
    {
        homeDraftLoaded = homeDescriptionExpanded = false; editGroupName = savedGroupName = savedHomeDescription = savedCurrencyName = "";
        ClearSelectedAnnouncements(); policyPointsDirty = policyLettersDirty = false;
    }
}
