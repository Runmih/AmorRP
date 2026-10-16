using System.Globalization;
using System.Text;

namespace AmorRP.Plugin.Services.Ui;

// Visual line breaks belong to the editor, never to persisted RP text. Offsets
// map both sides of a soft break to the same position in the original string.
internal sealed class WrappedTextProjection(string source, string display, int[] offsets)
{
    public string Source { get; } = source;
    public string Display { get; } = display;
    public int ToSource(int position) => offsets[Math.Clamp(position, 0, Display.Length)];
    public int ToDisplay(int position)
    {
        position = Math.Clamp(position, 0, Source.Length);
        var index = Array.BinarySearch(offsets, position);
        if (index < 0) index = ~index;
        while (index < Display.Length && offsets[index + 1] == position) index++;
        return index;
    }

    public static WrappedTextProjection Create(string text, float width, Func<string, float> measure)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        var ends = starts.Skip(1).Append(text.Length).ToArray();
        var elements = starts.Select((start, i) => text[start..ends[i]]).ToArray();
        var widths = elements.Select(x => x is "\r" or "\n" or "\r\n" ? 0 : measure(x)).ToArray();
        var sums = new float[widths.Length + 1];
        for (var i = 0; i < widths.Length; i++) sums[i + 1] = sums[i] + widths[i];
        var breaks = new HashSet<int>();
        int line = 0, word = 0;
        for (var i = 0; i < elements.Length; i++)
        {
            if (elements[i] is "\r" or "\n" or "\r\n") { line = word = i + 1; continue; }
            while (i > line && sums[i + 1] - sums[line] > Math.Max(1, width))
            {
                line = word > line ? word : i;
                breaks.Add(starts[line]); word = line;
            }
            if (string.IsNullOrWhiteSpace(elements[i])) word = i + 1;
        }
        var view = new StringBuilder(text.Length + breaks.Count);
        var map = new List<int>(text.Length + breaks.Count + 1) { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (breaks.Contains(i)) { view.Append('\n'); map.Add(i); }
            view.Append(text[i]); map.Add(i + 1);
        }
        return new(text, view.ToString(), map.ToArray());
    }

    public Edit Apply(string display, bool backspace = false, bool delete = false, int? cursor = null)
    {
        var prefix = 0;
        while (prefix < Display.Length && prefix < display.Length && Display[prefix] == display[prefix]) prefix++;
        int oldEnd = Display.Length, newEnd = display.Length;
        while (oldEnd > prefix && newEnd > prefix && Display[oldEnd - 1] == display[newEnd - 1]) { oldEnd--; newEnd--; }
        // Repeated characters/newlines make a diff ambiguous. Prefer the native
        // caret for a pure insertion/deletion, especially Enter at a soft break.
        var delta = display.Length - Display.Length;
        if (cursor.HasValue && delta != 0)
        {
            var at = delta > 0 ? cursor.Value - delta : cursor.Value;
            var removed = Math.Max(0, -delta); var inserted = Math.Max(0, delta);
            if (at >= 0 && at + removed <= Display.Length && at + inserted <= display.Length
                && Display.AsSpan(0, at).SequenceEqual(display.AsSpan(0, at))
                && Display.AsSpan(at + removed).SequenceEqual(display.AsSpan(at + inserted)))
            { prefix = at; oldEnd = at + removed; newEnd = at + inserted; }
        }
        var from = ToSource(prefix); var to = ToSource(oldEnd);
        // Backspace/Delete across a visual boundary should delete a character,
        // not consume a frame removing a line break that immediately reappears.
        if (from == to && oldEnd > prefix && newEnd == prefix && (backspace || delete))
        {
            var elements = StringInfo.ParseCombiningCharacters(Source);
            if (backspace && from > 0) from = elements.Last(x => x < from);
            else if (delete && to < Source.Length) to = elements.FirstOrDefault(x => x > to, Source.Length);
        }
        var insertion = display[prefix..newEnd];
        var text = Source[..from] + insertion + Source[to..];
        int Map(int position)
        {
            if (position < prefix) return Math.Min(ToSource(position), from);
            if (position <= newEnd) return from + position - prefix;
            return ToSource(position + oldEnd - newEnd) + insertion.Length - (to - from);
        }
        return new(text, Map);
    }
    internal sealed record Edit(string Text, Func<int, int> Position);
}
