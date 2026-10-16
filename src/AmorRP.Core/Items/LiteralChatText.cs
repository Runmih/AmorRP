using System.Text;
namespace AmorRP.Core.Items;

public static class LiteralChatText
{
    public const int DefaultCharacters = 50;
    public const int MaximumBytes = 200;
    public static string? Error(string? text, int maximum = DefaultCharacters)
    {
        var error = PlainText.Error(text, maximum);
        if (error != null) return error;
        if (Encoding.UTF8.GetByteCount(text!) > MaximumBytes) return $"Use at most {MaximumBytes} UTF-8 bytes; some characters use several bytes.";
        // FFXIV expands paired angle-bracket expressions. Unpaired punctuation,
        // including <3 and >:), remains literal and is preserved unchanged.
        var opening = text!.IndexOf('<');
        if (opening >= 0 && text.IndexOf('>', opening + 1) >= 0)
            return "Game placeholders such as <t> are not supported. Unpaired <3 is allowed.";
        return null;
    }
}
