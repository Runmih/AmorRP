using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Announcements;
using AmorRP.Plugin.Services.Api;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private bool homeDraftLoaded, homeDescriptionExpanded;
    private string editCommunityName = "", savedCommunityName = "", savedHomeDescription = "", savedCurrencyName = "";
    private void LoadHomeDrafts()
    {
        var current = community!;
        if (!homeDraftLoaded || editCommunityName == savedCommunityName) editCommunityName = savedCommunityName = current.Name;
        if (!homeDraftLoaded || editDescription == savedHomeDescription) editDescription = savedHomeDescription = current.Description;
        if (savedCommunityName == current.Name && savedHomeDescription == current.Description) editCommunityVersion = current.Version;
        if (!homeDraftLoaded || editCurrencyName == savedCurrencyName)
        { editCurrencyName = savedCurrencyName = current.Currency.Name; editCurrencyVersion = current.Currency.Version; }
        else if (savedCurrencyName == current.Currency.Name) editCurrencyVersion = current.Currency.Version;
        homeDraftLoaded = true;
    }
    private void DrawHomeProfile()
    {
        LoadHomeDrafts();
        var current = community!;
        var spacing = ImGui.GetTextLineHeightWithSpacing();
        var textWidth = Math.Max(100, ImGui.GetContentRegionAvail().X - 150);
        var descriptionHeight = ImGui.CalcTextSize(current.Description, false, textWidth).Y;
        var shownHeight = homeDescriptionExpanded ? descriptionHeight : Math.Min(descriptionHeight, spacing * 3);
        var height = CanGeneralModerate ? spacing * 4 + ImGui.GetFrameHeightWithSpacing() * 3 + 28 : Math.Max(128, shownHeight + spacing * 3 + 20);
        BeginHomeCard("CommunityProfile", height);
        if (ImGui.BeginTable("ProfileLayout", 2, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Logo", ImGuiTableColumnFlags.WidthFixed, 116);
            ImGui.TableSetupColumn("Profile", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextColumn(); DrawCommunityLogo(); ImGui.TableNextColumn(); ImGui.PushItemWidth(-1);
            if (Owner) LimitedInput("##HomeCommunityName", ref editCommunityName, 80, countCodeUnits: true);
            else ImGui.TextUnformatted(current.Name);
            if (CanGeneralModerate)
            {
                WrappedInput("##HomeDescription", ref editDescription, 8192, new(-1, spacing * 4), maximumCharacters: CommunityEdit.MaximumDescriptionCharacters);
                var conflict = savedCommunityName != current.Name || savedHomeDescription != current.Description;
                var error = string.IsNullOrWhiteSpace(editCommunityName) || editCommunityName.Length > 80 ? "Community name must contain 1–80 characters."
                    : AmorRP.Core.Items.PlainText.Characters(editDescription) > CommunityEdit.MaximumDescriptionCharacters ? "Description must be at most 300 characters." : null;
                if (error != null) Wrapped(error);
                if (conflict) { Wrapped("The saved profile changed elsewhere."); if (ImGui.Button("Reload profile")) { homeDraftLoaded = false; LoadHomeDrafts(); } }
                var dirty = editCommunityName != current.Name || editDescription != current.Description;
                if (dirty)
                {
                    ImGui.BeginDisabled(conflict || error != null);
                    if (ActionButton("Save profile")) Mutate("PATCH", $"api/v2/communities/{current.Id}", new CommunityEdit(Owner ? editCommunityName : current.Name, editDescription), ETag("community", current.Id, editCommunityVersion));
                    ImGui.EndDisabled();
                }
            }
            else
            {
                ImGui.BeginChild("CommunityDescription", new(0, Math.Max(spacing, shownHeight)), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                Wrapped(current.Description.Length == 0 ? "No community description yet." : current.Description); ImGui.EndChild();
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
        Wrapped(community!.MyBalance.Available + " " + community.Currency.Name);
        if (community.MyBalance.Reserved != "0") ImGui.TextDisabled(community.MyBalance.Reserved + " reserved for trading");
        if (CanGeneralModerate)
        {
            FieldLabel("Currency name"); LimitedInput("##HomeCurrencyName", ref editCurrencyName, 80, countCodeUnits: true);
            var conflict = savedCurrencyName != community.Currency.Name;
            if (conflict) { Wrapped("Currency name changed elsewhere."); if (ImGui.Button("Reload currency name")) { editCurrencyName = savedCurrencyName = community.Currency.Name; editCurrencyVersion = community.Currency.Version; } }
            if (editCurrencyName != community.Currency.Name)
            {
                ImGui.BeginDisabled(conflict || string.IsNullOrWhiteSpace(editCurrencyName) || editCurrencyName.Length > 80);
                if (ActionButton("Save currency name")) Mutate("PATCH", $"api/v2/communities/{community.Id}/currency", new CurrencyEdit(editCurrencyName, community.Currency.Symbol), ETag("currency", community.Currency.Id, editCurrencyVersion));
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
        if (community == null) return;
        if (request.Path == $"api/v2/communities/{community.Id}/logo")
        {
            var updated = JsonSerializer.Deserialize<CommandResult<Community>>(json, FoundationClient.Json)!.Result;
            community = community with { LogoAssetId = updated.LogoAssetId, LogoVersion = updated.LogoVersion };
            logos.Clear(); return;
        }
        if (request.Method == "PATCH" && request.Path == $"api/v2/communities/{community.Id}")
        {
            var updated = JsonSerializer.Deserialize<CommandResult<Community>>(json, FoundationClient.Json)!.Result;
            var submitted = JsonSerializer.Deserialize<CommunityEdit>(request.Json!, FoundationClient.Json)!;
            community = updated; communities = communities.Select(x => x.Id == updated.Id ? updated : x).ToArray();
            if (editCommunityName == submitted.Name) editCommunityName = updated.Name;
            if (editDescription == submitted.Description) editDescription = updated.Description;
            savedCommunityName = updated.Name; savedHomeDescription = updated.Description; editCommunityVersion = updated.Version;
        }
        if (request.Method == "PATCH" && request.Path == $"api/v2/communities/{community.Id}/currency")
        {
            var updated = JsonSerializer.Deserialize<CommandResult<AmorRP.Contracts.Currency.Currency>>(json, FoundationClient.Json)!.Result;
            var submitted = JsonSerializer.Deserialize<CurrencyEdit>(request.Json!, FoundationClient.Json)!;
            community = community with { Currency = updated };
            if (editCurrencyName == submitted.Name) editCurrencyName = updated.Name;
            savedCurrencyName = updated.Name; editCurrencyVersion = updated.Version;
        }
    }
    private void ClearHomeViews()
    {
        homeDraftLoaded = homeDescriptionExpanded = false; editCommunityName = savedCommunityName = savedHomeDescription = savedCurrencyName = "";
        ClearSelectedAnnouncements(); policyPointsDirty = policyLettersDirty = false;
    }
}
