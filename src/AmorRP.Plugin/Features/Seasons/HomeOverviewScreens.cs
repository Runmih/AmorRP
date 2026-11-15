using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private static void BeginHomeCard(string id, float height)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.055f, .06f, .075f, .94f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 12));
        ImGui.BeginChild(id, new(0, height), true);
    }
    private static void EndHomeCard() { ImGui.EndChild(); ImGui.PopStyleVar(); ImGui.PopStyleColor(); }
    private void DrawHomeOverview(bool editable = true)
    {
        if (logoRefresh.Error != null) DrawRefreshState(logoRefresh, "Season logo");
        DrawHomeProfile(editable); ImGui.Spacing();
        var lines = ImGui.GetTextLineHeightWithSpacing();
        var announcementHeight = Math.Max(160, (editable && CanAnnounce ? lines * 7 + ImGui.GetFrameHeightWithSpacing() * 3 : 0) + Math.Max(1, announcements.Length) * lines * 2.4f + 65);
        var resourcesHeight = (editable && CanGeneralModerate ? 280 : 200) + (CanCraftPotions ? lines * 2 : 0);
        if (ImGui.GetContentRegionAvail().X >= 620 && ImGui.BeginTable("HomeOverview", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Announcements", ImGuiTableColumnFlags.WidthStretch, .66f);
            ImGui.TableSetupColumn("Resources", ImGuiTableColumnFlags.WidthStretch, .34f);
            ImGui.TableNextColumn(); BeginHomeCard("AnnouncementsCard", Math.Max(announcementHeight, resourcesHeight)); DrawAnnouncements(editable); EndHomeCard();
            ImGui.TableNextColumn(); BeginHomeCard("ResourcesCard", Math.Max(announcementHeight, resourcesHeight)); DrawHomeResources(editable); EndHomeCard();
            ImGui.EndTable();
        }
        else
        {
            BeginHomeCard("AnnouncementsCard", announcementHeight); DrawAnnouncements(editable); EndHomeCard(); ImGui.Spacing();
            BeginHomeCard("ResourcesCard", resourcesHeight); DrawHomeResources(editable); EndHomeCard();
        }
    }
    private void DrawCommunityLogo(bool editable = true)
    {
        var size = new Vector2(104, 104); var position = ImGui.GetCursorScreenPos();
        ImGui.BeginDisabled(!editable || !Owner || WriteBlocked);
        var clicked = logos.HasImage ? ImGui.InvisibleButton("##CommunityLogo", size) : ImGui.Button(editable && Owner ? "Add logo" : "Season logo", size);
        if (logos.HasImage) logos.DrawInBox(position, size);
        if (editable && Owner && !WriteBlocked && ImGui.IsItemHovered())
        { ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); ImGui.GetWindowDrawList().AddRect(position, position + size, ImGui.GetColorU32(ImGuiCol.ButtonHovered)); }
        ImGui.EndDisabled();
        if (editable && Owner && ImGui.BeginPopupContextItem("CommunityLogoActions"))
        {
            ImGui.BeginDisabled(WriteBlocked || community!.LogoAssetId == null);
            if (ImGui.MenuItem("Remove logo")) Mutate("DELETE", $"api/v2/seasons/{community!.Id}/logo", null, ETag("season-logo", community.Id, community.LogoVersion));
            ImGui.EndDisabled(); ImGui.EndPopup();
        }
        if (editable && clicked) PickCommunityLogo();
    }
    private void PickCommunityLogo()
    {
        var communityId = community!.Id; var actor = session!.Character.Id; var uri = origin;
        var etag = ETag("season-logo", communityId, community.LogoVersion);
        files.OpenFileDialog("Season logo", ".png,.jpg,.jpeg,.webp", (ok, path) =>
        {
            if (!ok || community?.Id != communityId || session?.Character.Id != actor || origin != uri || !Owner || WriteBlocked) return;
            Start(async ct =>
            {
                var maximum = serviceLimits?.MaxCommunityLogoBytes ?? 2097152;
                await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
                if (file.Length <= 0 || file.Length > maximum) throw new InvalidOperationException($"Choose a PNG, JPG or WebP smaller than {maximum / 1024} KiB.");
                var bytes = new byte[(int)file.Length]; await file.ReadExactlyAsync(bytes, ct);
                return () => QueueUpload($"api/v2/seasons/{communityId}/logo", etag, bytes);
            }, "Reading season logo…");
        });
    }
}
