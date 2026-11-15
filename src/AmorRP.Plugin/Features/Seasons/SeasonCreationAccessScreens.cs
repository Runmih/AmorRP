using AmorRP.Contracts.Seasons;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private SeasonCreationPermissionPage seasonCreationAccess = new([], null);
    private bool seasonCreationAccessLoaded;
    private string seasonCreationSearch = "", seasonCreationReason = "";

    private void LoadSeasonCreationAccess(string? cursor = null)
    {
        if (!IsSeasonAdministrator || Busy) return;
        var path = "api/v2/seasons/creation-permissions?search=" + Uri.EscapeDataString(seasonCreationSearch.Trim())
            + (cursor == null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
        var token = session!.AccessToken;
        Start(async ct => {
            var page = await client.GetAsync<SeasonCreationPermissionPage>(origin!, path, token, ct);
            return () => {
                seasonCreationAccess = cursor == null ? page : new([..seasonCreationAccess.Items, ..page.Items], page.NextCursor);
                seasonCreationAccessLoaded = true;
            };
        }, "Loading season creation permissions…");
    }

    private void DrawSeasonCreationAccess()
    {
        if (!IsSeasonAdministrator) return;
        Wrapped("Allow a verified character to create seasons. Activation, archival and permission grants remain administrator actions.");
        if (!seasonCreationAccessLoaded && !Busy) LoadSeasonCreationAccess();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##SeasonCreatorSearch", "Search characters…", ref seasonCreationSearch, 400, ImGuiInputTextFlags.EnterReturnsTrue))
            LoadSeasonCreationAccess();
        ImGui.BeginDisabled(Busy);
        if (ImGui.Button("Search / refresh")) LoadSeasonCreationAccess();
        ImGui.EndDisabled();
        FieldLabel("Reason for permission changes");
        ImGui.SetNextItemWidth(-1); ImGui.InputText("##SeasonCreatorReason", ref seasonCreationReason, 501);
        foreach (var row in seasonCreationAccess.Items)
        {
            ImGui.PushID(row.Character.Id.ToString());
            ImGui.Separator(); ImGui.TextUnformatted(row.Character.DisplayName + " @ " + row.Character.HomeWorldName);
            if (row.IsAdministrator) ImGui.TextDisabled("Can create seasons · Administrator default");
            else
            {
                var allowed = row.CanCreate;
                ImGui.BeginDisabled(WriteBlocked || Busy || string.IsNullOrWhiteSpace(seasonCreationReason) || seasonCreationReason.Length > 500);
                if (ImGui.Checkbox("May create seasons", ref allowed))
                    Mutate("PUT", $"api/v2/seasons/creation-permissions/{row.Character.Id}", new SeasonCreationPermissionEdit(allowed, seasonCreationReason), row.Etag);
                ImGui.EndDisabled();
            }
            ImGui.PopID();
        }
        if (seasonCreationAccess.NextCursor != null && !Busy && ImGui.Button("More characters"))
            LoadSeasonCreationAccess(seasonCreationAccess.NextCursor);
    }
}
