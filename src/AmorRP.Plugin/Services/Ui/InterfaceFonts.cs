using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;

namespace AmorRP.Plugin.Services.Ui;

internal sealed class InterfaceFonts(IFontAtlas atlas) : IDisposable
{
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

    public void Dispose() => signature.Dispose();
}
