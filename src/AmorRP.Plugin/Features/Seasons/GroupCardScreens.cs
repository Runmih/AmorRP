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
    private readonly Dictionary<Guid, GroupCardLogo> groupCardLogos = [];
    private long groupCardLogoUse;

    private sealed record GroupCardLogo(Guid AssetId, int Version, SeasonImageCache Image, BackgroundRefresh<byte[]> Download) : IDisposable
    {
        public long LastUsed { get; set; }

        public void Dispose()
        {
            Download.Dispose();
            Image.Dispose();
        }
    }

    private void ClearGroupCardLogos()
    {
        foreach (var logo in groupCardLogos.Values) logo.Dispose();
        groupCardLogos.Clear();
    }

    private SeasonImageCache? ReadGroupCardLogo(Group? group, DateTimeOffset now)
    {
        if (group?.LogoAssetId is not Guid assetId || session == null || origin == null) return null;
        if (groupCardLogos.TryGetValue(group.Id, out var logo) && (logo.AssetId != assetId || logo.Version != group.LogoVersion))
        {
            logo.Dispose();
            groupCardLogos.Remove(group.Id);
            logo = null;
        }
        if (logo == null)
        {
            if (groupCardLogos.Values.Count(entry => entry.Download.IsLoading) >= 3) return null;
            if (groupCardLogos.Count >= 32)
            {
                var oldest = groupCardLogos.MinBy(pair => pair.Value.LastUsed);
                oldest.Value.Dispose();
                groupCardLogos.Remove(oldest.Key);
            }
            logo = new(assetId, group.LogoVersion, new(textures, "group card logo"), new(TimeSpan.FromMinutes(1)));
            groupCardLogos.Add(group.Id, logo);
        }
        logo.LastUsed = ++groupCardLogoUse;
        if (!logo.Download.HasLoaded && groupCardLogos.Values.Count(entry => entry.Download.IsLoading) < 3)
        {
            var uri = origin;
            var token = session.AccessToken;
            logo.Download.Start(now, 0, requests.Token,
                ct => client.GetImageAsync(uri, GroupPath(group) + "/logo", token, 1048576, ct));
        }
        return logo.Image;
    }

    private void RefreshGroupCardLogos(DateTimeOffset now)
    {
        foreach (var logo in groupCardLogos.Values)
        {
            if (logo.Download.TryCollect(now, 0, out var bytes)) logo.Image.Update(logo.AssetId.ToString(), bytes, true);
            logo.Image.Draw();
        }
    }

    private sealed record EventCardDetails(string Schedule, string Location);

    private void DrawGroupCard(string childId, Guid id, string title, string groupName, Group? group, DateTimeOffset now,
        IReadOnlyList<string> body, string reward = "", EventCardDetails? eventDetails = null)
    {
        var padding = ImGui.GetFontSize() * .8f;
        var stamp = ImGui.GetFontSize() * 2.7f;
        var gap = ImGui.GetStyle().ItemSpacing;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X - padding * 2 - 4);
        var signature = "— " + groupName;
        var linkWidth = group == null ? 0 : ImGui.GetFontSize() + gap.X;
        var signatureWidth = Math.Max(1, width - stamp - gap.X - linkWidth);
        Vector2 signatureSize;
        using (interfaceFonts.PushSignature(signature))
            signatureSize = ImGui.CalcTextSize(signature, false, signatureWidth);

        var footerHeight = Math.Max(stamp, signatureSize.Y);
        var lines = ImGui.GetTextLineHeightWithSpacing();
        var fieldGap = ImGui.GetFontSize() * 1.4f;
        var sealWidth = stamp + gap.X + signatureSize.X + linkWidth;
        var shareFooter = eventDetails != null && width - sealWidth - fieldGap >= ImGui.GetFontSize() * 26;
        var detailsWidth = shareFooter ? width - sealWidth - fieldGap : width;
        var hasLocation = eventDetails != null && !string.IsNullOrWhiteSpace(eventDetails.Location);
        var detailColumns = detailsWidth >= ImGui.GetFontSize() * 23;
        var dateWidth = detailColumns && hasLocation
            ? Math.Min(ImGui.CalcTextSize(eventDetails!.Schedule).X + 1, (detailsWidth - fieldGap) * .55f)
            : detailsWidth;
        var locationWidth = Math.Max(1, detailsWidth - dateWidth - fieldGap);
        var dateHeight = eventDetails == null ? 0 : CardFieldHeight("Date & Time", eventDetails.Schedule, dateWidth, detailColumns);
        var locationHeight = hasLocation ? CardFieldHeight("Location", eventDetails!.Location, detailColumns ? locationWidth : detailsWidth, detailColumns) : 0;
        var detailsHeight = detailColumns ? Math.Max(dateHeight, locationHeight)
            : dateHeight + (hasLocation ? gap.Y + locationHeight : 0);
        if (shareFooter) footerHeight = Math.Max(footerHeight, detailsHeight);
        var height = padding * 2 + ImGui.CalcTextSize(title, false, width).Y
            + body.Sum(line => ImGui.CalcTextSize(line, false, width).Y) + footerHeight + gap.Y * (body.Count + 4) + 4;
        if (eventDetails != null && !shareFooter) height += detailsHeight + gap.Y * 2;
        if (reward.Length > 0) height += lines + ImGui.CalcTextSize(reward, false, width).Y + gap.Y * 2;

        ImGui.PushID(id.ToString());
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(.112f, .089f, .063f, 1));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.46f, .36f, .22f, .9f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(padding));
        if (ImGui.BeginChild(childId, new Vector2(0, height), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, InventoryGold);
            Wrapped(title);
            ImGui.PopStyleColor();
            ImGui.Spacing();
            foreach (var line in body) Wrapped(line);
            if (reward.Length > 0)
            {
                ImGui.Spacing();
                UiIcons.Draw(FontAwesomeIcon.Gem);
                ImGui.SameLine();
                ImGui.TextUnformatted("Offered reward");
                Wrapped(reward);
            }

            ImGui.Spacing();
            if (eventDetails != null)
            {
                var top = ImGui.GetCursorScreenPos();
                DrawCardField(top, "Date & Time", eventDetails.Schedule, dateWidth, detailColumns);
                if (hasLocation)
                {
                    var position = top + (detailColumns ? new Vector2(dateWidth + fieldGap, 0) : new Vector2(0, dateHeight + gap.Y));
                    DrawCardField(position, "Location", eventDetails.Location, detailColumns ? locationWidth : detailsWidth, detailColumns);
                }
                if (!shareFooter)
                {
                    ImGui.Dummy(new Vector2(0, detailsHeight));
                    ImGui.Spacing();
                }
            }

            var logo = ReadGroupCardLogo(group, now);
            DrawGroupCardSeal(groupName, group, logo, signature, signatureSize, signatureWidth, stamp, footerHeight, linkWidth);
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
        ImGui.PopID();
    }

    private static Vector2 CardFieldValueOffset(string label, float width, bool separateLines)
    {
        var labelWidth = ImGui.CalcTextSize(label).X + ImGui.GetStyle().ItemSpacing.X;
        return separateLines || width - labelWidth < ImGui.GetFontSize() * 8
            ? new(0, ImGui.GetTextLineHeightWithSpacing())
            : new(labelWidth, 0);
    }

    private static float CardFieldHeight(string label, string value, float width, bool separateLines)
    {
        var offset = CardFieldValueOffset(label, width, separateLines);
        return offset.Y + ImGui.CalcTextSize(value, false, Math.Max(1, width - offset.X)).Y;
    }

    private static void DrawCardField(Vector2 position, string label, string value, float width, bool separateLines)
    {
        var offset = CardFieldValueOffset(label, width, separateLines);
        var draw = ImGui.GetWindowDrawList();
        draw.AddText(position, ImGui.GetColorU32(InventoryGold), label);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), position + offset, ImGui.GetColorU32(ImGuiCol.Text),
            value, Math.Max(1, width - offset.X));
    }

    private void DrawGroupCardSeal(string groupName, Group? group, SeasonImageCache? logo, string signature,
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
            var initial = StringInfo.GetNextTextElement(groupName);
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
