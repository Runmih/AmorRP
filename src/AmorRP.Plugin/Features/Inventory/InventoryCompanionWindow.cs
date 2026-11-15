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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(.075f, .083f, .10f, .98f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.48f, .40f, .25f, .8f));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(.32f, .27f, .16f, .85f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(.42f, .35f, .20f, .9f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(.49f, .40f, .22f, 1));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(.12f, .13f, .15f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(.32f, .27f, .16f, 1));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(.42f, .35f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(.22f, .23f, .25f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.38f, .32f, .20f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.49f, .40f, .22f, 1));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.12f, .13f, .15f, 1));
    }

    public override void PostDraw() { ImGui.PopStyleColor(12); ImGui.PopStyleVar(2); }

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
