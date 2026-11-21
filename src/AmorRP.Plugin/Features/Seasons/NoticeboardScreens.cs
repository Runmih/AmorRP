using System.Globalization;
using System.Numerics;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Media;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private string noticeSearch = "";
    private readonly Dictionary<Guid, NoticeLogo> noticeLogos = [];
    private long noticeLogoUse;

    private sealed record NoticeLogo(Guid AssetId, int Version, SeasonImageCache Image, BackgroundRefresh<byte[]> Download) : IDisposable
    {
        public long LastUsed { get; set; }

        public void Dispose()
        {
            Download.Dispose();
            Image.Dispose();
        }
    }

    private void ClearNoticeLogos()
    {
        foreach (var logo in noticeLogos.Values) logo.Dispose();
        noticeLogos.Clear();
    }

    private SeasonImageCache? ReadNoticeLogo(Group? group, DateTimeOffset now)
    {
        if (group?.LogoAssetId is not Guid assetId || session == null || origin == null) return null;
        if (noticeLogos.TryGetValue(group.Id, out var logo) && (logo.AssetId != assetId || logo.Version != group.LogoVersion))
        {
            logo.Dispose();
            noticeLogos.Remove(group.Id);
            logo = null;
        }
        if (logo == null)
        {
            if (noticeLogos.Values.Count(entry => entry.Download.IsLoading) >= 3) return null;
            if (noticeLogos.Count >= 32)
            {
                var oldest = noticeLogos.MinBy(pair => pair.Value.LastUsed);
                oldest.Value.Dispose();
                noticeLogos.Remove(oldest.Key);
            }
            logo = new(assetId, group.LogoVersion, new(textures, "noticeboard logo"), new(TimeSpan.FromMinutes(1)));
            noticeLogos.Add(group.Id, logo);
        }
        logo.LastUsed = ++noticeLogoUse;
        if (!logo.Download.HasLoaded && noticeLogos.Values.Count(entry => entry.Download.IsLoading) < 3)
        {
            var uri = origin;
            var token = session.AccessToken;
            logo.Download.Start(now, 0, requests.Token,
                ct => client.GetImageAsync(uri, GroupPath(group) + "/logo", token, 1048576, ct));
        }
        return logo.Image;
    }

    private void DrawNoticeboard()
    {
        if (community == null) { DrawNoSeason(); return; }
        DrawRefreshState(hubRefresh, "Noticeboard");
        UiIcons.SearchLabel();
        ImGui.SetNextItemWidth(Math.Max(70, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 1.6f));
        LimitedInput("##FilterNotices", ref noticeSearch, 100, hint: "Filter notices…");
        ImGui.SameLine();
        ImGui.BeginDisabled(noticeSearch.Length == 0);
        if (UiIcons.ClearButton("Clear notice filter")) noticeSearch = "";
        ImGui.EndDisabled();

        var now = DateTimeOffset.UtcNow;
        foreach (var logo in noticeLogos.Values)
        {
            if (logo.Download.TryCollect(now, 0, out var bytes)) logo.Image.Update(logo.AssetId.ToString(), bytes, true);
            logo.Image.Draw();
        }
        var found = false;
        foreach (var item in hub.Notices)
        {
            if (!(item.Title + " " + item.Message + " " + item.GroupName + " " + item.Reward)
                .Contains(noticeSearch, StringComparison.OrdinalIgnoreCase)) continue;
            found = true;
            DrawNoticeCard(item, communityGroups.FirstOrDefault(group => group.Id == item.GroupId), now);
        }
        if (hub.Notices.Length == 0) Wrapped("No requests posted yet. Group owners and authorized staff can publish one from their group's Notice tab.");
        else if (!found) ImGui.TextDisabled("No matching notices.");
    }

    private void DrawNoticeCard(GroupNotice item, Group? group, DateTimeOffset now)
    {
        var padding = ImGui.GetFontSize() * .8f;
        var stamp = ImGui.GetFontSize() * 2.7f;
        var gap = ImGui.GetStyle().ItemSpacing;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - padding * 2 - 4);
        var signature = "— " + item.GroupName;
        var linkWidth = group == null ? 0 : ImGui.GetFontSize() + gap.X;
        var signatureWidth = Math.Max(1, width - stamp - gap.X - linkWidth);
        Vector2 signatureSize;
        using (interfaceFonts.PushSignature(signature))
            signatureSize = ImGui.CalcTextSize(signature, false, signatureWidth);

        var footerHeight = Math.Max(stamp, signatureSize.Y);
        var lines = ImGui.GetTextLineHeightWithSpacing();
        var height = padding * 2 + ImGui.CalcTextSize(item.Title, false, width).Y
            + ImGui.CalcTextSize(item.Message, false, width).Y + footerHeight + gap.Y * 5 + 4;
        if (item.Reward.Length > 0) height += lines + ImGui.CalcTextSize(item.Reward, false, width).Y + gap.Y * 2;

        ImGui.PushID(item.GroupId.ToString());
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.112f, .089f, .063f, 1));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.46f, .36f, .22f, .9f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding));
        if (ImGui.BeginChild("NoticeCard", new Vector2(0, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, InventoryGold);
            Wrapped(item.Title);
            ImGui.PopStyleColor();
            ImGui.Spacing();
            Wrapped(item.Message);
            if (item.Reward.Length > 0)
            {
                ImGui.Spacing();
                UiIcons.Draw(FontAwesomeIcon.Gem);
                ImGui.SameLine();
                ImGui.TextUnformatted("Offered reward");
                Wrapped(item.Reward);
            }

            ImGui.Spacing();
            var logo = ReadNoticeLogo(group, now);
            DrawNoticeSeal(item, group, logo, signature, signatureSize, signatureWidth, stamp, footerHeight, linkWidth);
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
        ImGui.PopID();
    }

    private void DrawNoticeSeal(GroupNotice item, Group? group, SeasonImageCache? logo, string signature,
        Vector2 signatureSize, float signatureWidth, float stamp, float height, float linkWidth)
    {
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var width = stamp + gap + signatureSize.X + linkWidth;
        var position = ImGui.GetCursorScreenPos();
        position.X += Math.Max(0, ImGui.GetContentRegionAvail().X - width);
        ImGui.SetCursorScreenPos(position);
        ImGui.BeginDisabled(group == null);
        if (ImGui.InvisibleButton("View group", new Vector2(width, height)) && group != null)
        {
            requestedDirectoryGroup = group.Id;
            requestedCommunityPage = CommunityBrowserPage.Groups;
        }
        var hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            ImGui.SetTooltip("View group");
        }
        ImGui.EndDisabled();

        var draw = ImGui.GetWindowDrawList();
        var stampPosition = position + new Vector2(0, (height - stamp) * .5f);
        var stampSize = new Vector2(stamp);
        draw.AddRect(stampPosition, stampPosition + stampSize, ImGui.GetColorU32(InventoryGold), stamp * .18f);
        if (logo?.HasImage == true)
        {
            var inset = stamp * .1f;
            logo.DrawInBox(stampPosition + new Vector2(inset), stampSize - new Vector2(inset * 2));
        }
        else
        {
            var initial = StringInfo.GetNextTextElement(item.GroupName);
            draw.AddText(stampPosition + (stampSize - ImGui.CalcTextSize(initial)) * .5f,
                ImGui.GetColorU32(InventoryGold), initial);
        }
        var textColor = ImGui.GetColorU32(hovered ? ImGuiCol.Text : ImGuiCol.TextDisabled);
        if (group != null)
        {
            var iconFont = UiBuilder.IconFont;
            var arrowPosition = position + new Vector2(width - linkWidth + gap, (height - iconFont.FontSize) * .5f);
            draw.AddText(iconFont, iconFont.FontSize, arrowPosition, textColor, FontAwesomeIcon.ExternalLinkAlt.ToIconString());
        }
        using var handwritten = interfaceFonts.PushSignature(signature);
        var signaturePosition = position + new Vector2(stamp + gap, (height - signatureSize.Y) * .5f);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), signaturePosition, textColor, signature, signatureWidth);
    }
}
