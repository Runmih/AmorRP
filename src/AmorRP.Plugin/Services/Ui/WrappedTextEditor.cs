using System.Numerics;
using System.Text;
using AmorRP.Core.Items;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Services.Ui;

// The API 15 multiline input has no native word wrapping. Keep its keyboard,
// selection and IME handling, projecting soft breaks into a display-only buffer.
internal sealed class WrappedTextEditor
{
    private readonly Dictionary<uint, State> editors = [];
    public void Clear() => editors.Clear();
    public void Draw(string label, ref string text, int maxBytes, Vector2 size, bool readOnly = false, int maximumCharacters = int.MaxValue)
    {
        var id = ImGui.GetID(label);
        if (!editors.TryGetValue(id, out var state)) editors[id] = state = new(text);
        var externalChange = state.Fresh || state.Text != text;
        state.Fresh = false;
        if (externalChange) { state.Text = text; state.Cursor = state.Start = state.End = 0; state.Undo.Clear(); state.Redo.Clear(); }
        var width = Math.Max(1, (size.X <= 0 ? ImGui.GetContentRegionAvail().X + size.X : size.X)
            - ImGui.GetStyle().FramePadding.X * 2 - ImGui.GetStyle().ScrollbarSize - 2);
        // Glyph advances are font/scale dependent; cache only for this draw.
        var measures = new Dictionary<string, float>();
        float Measure(string value)
        {
            if (!measures.TryGetValue(value, out var result)) measures[value] = result = ImGui.CalcTextSize(value).X;
            return result;
        }
        var font = ImGui.GetFont(); var fontSize = ImGui.GetFontSize();
        void Reflow()
        {
            if (state.Projection.Source == state.Text && state.Width == width && state.Font.Equals(font) && state.FontSize == fontSize) return;
            state.Projection = WrappedTextProjection.Create(state.Text, width, Measure);
            state.Width = width; state.Font = font; state.FontSize = fontSize;
        }
        var active = ImGuiP.GetActiveID() == id;
        if (!active || externalChange) Reflow();
        var view = state.Projection.Display;
        var flags = ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.NoHorizontalScroll | ImGuiInputTextFlags.NoUndoRedo;
        if (readOnly) flags |= ImGuiInputTextFlags.ReadOnly;
        var control = ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyAlt;
        var undo = active && !readOnly && control && ImGui.IsKeyPressed(ImGuiKey.Z, false);
        var redo = active && !readOnly && control && ImGui.IsKeyPressed(ImGuiKey.Y, false);
        if (undo && ImGui.GetIO().KeyShift) { undo = false; redo = true; }
        var copy = active && ((control && (ImGui.IsKeyPressed(ImGuiKey.C, false) || ImGui.IsKeyPressed(ImGuiKey.X, false) || ImGui.IsKeyPressed(ImGuiKey.Insert, false)))
            || (ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.Delete, false)));
        var copied = copy ? state.Text[Math.Min(state.Start, state.End)..Math.Max(state.Start, state.End)] : null;
        var backspace = ImGui.IsKeyPressed(ImGuiKey.Backspace);
        var delete = ImGui.IsKeyPressed(ImGuiKey.Delete);
        int Callback(ref ImGuiInputTextCallbackData data)
        {
            var edited = Encoding.UTF8.GetString(data.BufTextSpan);
            var cursor = Utf16Position(edited, data.CursorPos);
            var start = Utf16Position(edited, data.SelectionStart);
            var end = Utf16Position(edited, data.SelectionEnd);
            if (!externalChange && !readOnly && edited != state.Projection.Display)
            {
                var edit = state.Projection.Apply(edited, backspace, delete, cursor);
                var limited = TextInputLimit.Apply(state.Text, edit.Text, maximumCharacters, maxBytes - 1);
                if (limited.Text != state.Text)
                {
                    state.Remember(state.Undo); state.Redo.Clear(); state.Text = limited.Text;
                }
                cursor = limited.Position(edit.Position(cursor)); start = limited.Position(edit.Position(start)); end = limited.Position(edit.Position(end));
            }
            else { cursor = state.Projection.ToSource(cursor); start = state.Projection.ToSource(start); end = state.Projection.ToSource(end); }
            state.Cursor = cursor; state.Start = start; state.End = end;
            if (undo) state.Restore(state.Undo, state.Redo);
            else if (redo) state.Restore(state.Redo, state.Undo);
            if (undo || redo)
            {
                var limited = TextInputLimit.Apply("", state.Text, maximumCharacters, maxBytes - 1);
                state.Text = limited.Text; state.Cursor = limited.Position(state.Cursor);
                state.Start = limited.Position(state.Start); state.End = limited.Position(state.End);
            }
            Reflow();
            var bytes = Encoding.UTF8.GetBytes(state.Projection.Display);
            var rewrite = !data.BufTextSpan.SequenceEqual(bytes);
            if (rewrite)
            {
                bytes.CopyTo(data.BufSpan); data.BufSpan[bytes.Length] = 0;
                data.BufTextLen = bytes.Length; data.BufDirty = 1;
            }
            int Utf8(int offset) => Encoding.UTF8.GetByteCount(state.Projection.Display.AsSpan(0, state.Projection.ToDisplay(offset)));
            if (rewrite || externalChange || undo || redo)
            { data.CursorPos = Utf8(state.Cursor); data.SelectionStart = Utf8(state.Start); data.SelectionEnd = Utf8(state.End); }
            return 0;
        }
        ImGui.InputTextMultiline(label, ref view, checked(maxBytes * 2 + 1), size, flags, Callback);
        if (copied is { Length: > 0 }) ImGui.SetClipboardText(copied);
        text = state.Text;
    }
    private static int Utf16Position(string value, int utf8) => Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value).AsSpan(0, Math.Clamp(utf8, 0, Encoding.UTF8.GetByteCount(value)))).Length;
    private sealed record Snapshot(string Text, int Cursor, int Start, int End);
    private sealed class State(string text)
    {
        public string Text = text;
        public bool Fresh = true;
        public float Width = float.NaN, FontSize;
        public ImFontPtr Font;
        public WrappedTextProjection Projection = WrappedTextProjection.Create(text, float.MaxValue, _ => 0);
        public int Cursor, Start, End;
        public readonly List<Snapshot> Undo = [], Redo = [];
        public void Remember(List<Snapshot> stack)
        { if (stack.Count == 100) stack.RemoveAt(0); stack.Add(new(Text, Cursor, Start, End)); }
        public void Restore(List<Snapshot> from, List<Snapshot> to)
        {
            if (from.Count == 0) return;
            Remember(to); var value = from[^1]; from.RemoveAt(from.Count - 1);
            Text = value.Text; Cursor = value.Cursor; Start = value.Start; End = value.End;
        }
    }
}
