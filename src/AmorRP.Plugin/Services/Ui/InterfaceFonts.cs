using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;

namespace AmorRP.Plugin.Services.Ui;

internal sealed class InterfaceFonts(IFontAtlas atlas) : IDisposable
{
    private readonly IFontHandle labels = atlas.NewDelegateFontHandle(step =>
        step.OnPreBuild(toolkit => toolkit.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx)));

    private readonly IFontHandle signature = atlas.NewDelegateFontHandle(step => step.OnPreBuild(toolkit =>
    {
        using var resource = typeof(InterfaceFonts).Assembly.GetManifestResourceStream(
            "AmorRP.Plugin.Assets.Fonts.Caveat-Regular.ttf")!;
        using var data = new MemoryStream();
        resource.CopyTo(data);
        toolkit.AddFontFromMemory(data.ToArray(), new SafeFontConfig
        {
            SizePx = UiBuilder.DefaultFontSizePx * 1.6f,
            GlyphRanges = [0x0020, 0x024f, 0x0400, 0x052f, 0x1e00, 0x1eff, 0x2000, 0x206f, 0]
        }, "Caveat Regular");
    }));

    public bool BeginTab(FontAwesomeIcon icon, string label, ImGuiTabItemFlags flags)
    {
        using var font = labels.Push();
        return ImGui.BeginTabItem(Label(icon, label), flags);
    }

    public bool SmallButton(FontAwesomeIcon icon, string label)
    {
        using var font = labels.Push();
        return ImGui.SmallButton(Label(icon, label));
    }

    private string Label(FontAwesomeIcon icon, string label) =>
        labels.Available ? $"{icon.ToIconString()}  {label}###{label}" : $"{label}###{label}";

    public unsafe IDisposable? PushSignature(string text)
    {
        using var loaded = signature.TryLock(out _);
        if (loaded == null) return null;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!rune.IsBmp || loaded.ImFont.FindGlyphNoFallback((ushort)rune.Value) == null) return null;
        }
        return signature.Push();
    }

    public void Dispose()
    {
        labels.Dispose();
        signature.Dispose();
    }
}
