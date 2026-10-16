using System.Globalization;
using System.Text;
namespace AmorRP.Core.Items;

// Shared pure text rules; API authorization and accounting remain server-owned.
public static class PlainText
{
    public static int Characters(string text) => new StringInfo(text).LengthInTextElements;
    public static string? Error(string? text, int maximum, bool multiline = false, bool required = true)
    {
        if (text == null || required && string.IsNullOrWhiteSpace(text)) return "Enter some text.";
        if (Characters(text) > maximum) return $"Use at most {maximum} characters.";
        for (var i = 0; i < text.Length;)
        {
            if (!Rune.TryGetRuneAt(text, i, out var rune)) return "Remove unsupported characters.";
            i += rune.Utf16SequenceLength;
            if (multiline && rune.Value == '\n') continue;
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate)
                return multiline ? "Remove hidden or unsupported characters; use ordinary line breaks." : "Use one line without hidden or control characters.";
        }
        return null;
    }
}
