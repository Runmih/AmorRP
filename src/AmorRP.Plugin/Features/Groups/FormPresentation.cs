using System.Numerics;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Ui;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private readonly WrappedTextEditor textEditor = new();
    private void WrappedInput(string id, ref string text, int maxBytes, Vector2 size, bool readOnly = false, int maximumCharacters = int.MaxValue) => textEditor.Draw(id, ref text, maxBytes, size, readOnly, maximumCharacters);
    private static bool LimitedInput(string id, ref string text, int maximumCharacters, int maxBytes = 8192, bool countCodeUnits = false)
    {
        var previous = text;
        int Callback(ref ImGuiInputTextCallbackData data)
        {
            var candidate = System.Text.Encoding.UTF8.GetString(data.BufTextSpan);
            var limited = TextInputLimit.Apply(previous, candidate, maximumCharacters, maxBytes - 1, countCodeUnits ? maximumCharacters : int.MaxValue);
            if (candidate == limited.Text) return 0;
            int Position(int offset) => System.Text.Encoding.UTF8.GetByteCount(limited.Text.AsSpan(0,
                limited.Position(System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(candidate).AsSpan(0, offset)).Length)));
            var cursor = Position(data.CursorPos); var start = Position(data.SelectionStart); var end = Position(data.SelectionEnd);
            var bytes = System.Text.Encoding.UTF8.GetBytes(limited.Text);
            bytes.CopyTo(data.BufSpan); data.BufSpan[bytes.Length] = 0;
            data.BufTextLen = bytes.Length; data.BufDirty = 1;
            data.CursorPos = cursor; data.SelectionStart = start; data.SelectionEnd = end;
            return 0;
        }
        return ImGui.InputText(id, ref text, maxBytes, ImGuiInputTextFlags.CallbackEdit, Callback);
    }
    private int ChatLimit => serviceLimits?.ChatMessageTextElements ?? LiteralChatText.DefaultCharacters;
    private static void FieldLabel(string label) { ImGui.TextUnformatted(label); ImGui.SetNextItemWidth(-1); }
    // Reserve space for validation so typing a valid/invalid value does not move
    // the fields below it. Explanations stay inline rather than in hover boxes.
    private static void FieldNote(string id, string? error, string hint)
    {
        ImGui.BeginChild("FieldNote" + id, new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 2.2f), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PushStyleColor(ImGuiCol.Text, error == null ? ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled] : new Vector4(1, .55f, .45f, 1));
        ImGui.TextWrapped(error ?? hint);
        ImGui.PopStyleColor(); ImGui.EndChild();
    }
    private static string TypeName(string type) => type switch { "potion" => "Potion", "letter" => "Letter", "book" => "Book", "note" => "Note", _ => type };
}
