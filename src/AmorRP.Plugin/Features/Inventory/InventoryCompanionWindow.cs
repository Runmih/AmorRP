using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Inventory;

public sealed class InventoryCompanionWindow : Window
{
    private static readonly string[] InventoryAddons = ["InventoryExpansion", "InventoryLarge", "Inventory"];
    private readonly IGameGui gameGui;
    private readonly Func<bool> enabled;
    private readonly Action draw;
    private InventoryAttachmentLayout? layout;

    public InventoryCompanionWindow(IGameGui gameGui, Func<bool> enabled, Action draw)
        : base("Amor RP inventory###AmorRPInventoryCompanion", ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollbar)
    {
        this.gameGui = gameGui; this.enabled = enabled; this.draw = draw;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    // Look up fresh wrappers every frame: closing/reopening or changing layouts can
    // destroy the native addon. No native pointers or event hooks are retained.
    public bool UpdateAttachment()
    {
        layout = null;
        if (enabled() && !gameGui.GameUiHidden)
        {
            foreach (var name in InventoryAddons)
            {
                var addon = gameGui.GetAddonByName(name);
                if (!addon.IsReady || !addon.IsVisible) continue;
                var viewport = ImGui.GetMainViewport();
                // Keep controls readable when native UI is smaller than Dalamud text.
                var widthScale = Math.Max(addon.Scale, ImGui.GetFontSize() / 17f);
                layout = InventoryAttachmentLayout.Place(addon.Position + viewport.Pos, addon.ScaledSize,
                    viewport.WorkPos, viewport.WorkSize, Math.Max(widthScale, .5f));
                break;
            }
        }
        IsOpen = layout.HasValue;
        return IsOpen;
    }

    public override void PreDraw()
    {
        if (layout is not { } placement) return;
        ImGui.SetNextWindowPos(placement.Position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(placement.Size, ImGuiCond.Always);
    }

    public override void Draw()
    {
        if (ImGui.GetContentRegionAvail().X < ImGui.GetFontSize() * 23)
        {
            ImGui.TextWrapped("Move inventory further left to make room for your RP inventory.");
            return;
        }
        draw();
    }
}
