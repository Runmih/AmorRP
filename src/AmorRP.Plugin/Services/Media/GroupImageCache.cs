using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Services.Media;

// One current group image; scoped to server, character, group and asset revision.
public sealed class GroupImageCache(ITextureProvider textures, string label, bool coinFallback = false) : IDisposable
{
    private string key = "";
    private IDalamudTextureWrap? current;
    private Task<IDalamudTextureWrap>? pending;
    private CancellationTokenSource loading = new();
    public bool Matches(string candidate) => key == candidate;
    public void Update(string candidate, byte[]? png, bool custom)
    {
        if (key == candidate) return;
        Clear(); key = candidate;
        if (!custom && coinFallback)
        {
            using var resource = typeof(GroupImageCache).Assembly.GetManifestResourceStream("AmorRP.Plugin.Assets.default-coin.png");
            using var bytes = new MemoryStream(); resource?.CopyTo(bytes); png = bytes.ToArray();
        }
        if (png is { Length: > 0 }) pending = textures.CreateFromImageAsync(png, "AmorRP " + label, loading.Token);
    }
    public void Draw()
    {
        if (pending is not { IsCompleted: true }) return;
        if (pending.IsCompletedSuccessfully) current = pending.Result;
        else _ = pending.Exception;
        pending = null;
    }
    public bool HasImage => current != null;
    public void DrawInBox(System.Numerics.Vector2 position, System.Numerics.Vector2 size)
    {
        if (current == null) return;
        var scale = Math.Min(size.X / current.Width, size.Y / current.Height);
        var fitted = new System.Numerics.Vector2(current.Width * scale, current.Height * scale);
        var top = position + (size - fitted) / 2;
        ImGui.GetWindowDrawList().AddImage(current.Handle, top, top + fitted);
    }
    public void DrawIcon() { if (current != null) ImGui.Image(current.Handle, new(24, 24)); }
    public void Clear()
    {
        loading.Cancel(); loading.Dispose(); loading = new();
        current?.Dispose(); current = null; key = "";
        if (pending != null) _ = pending.ContinueWith(t => { if (t.IsCompletedSuccessfully) t.Result.Dispose(); else _ = t.Exception; }, TaskScheduler.Default);
        pending = null;
    }
    public void Dispose() { Clear(); loading.Dispose(); }
}
