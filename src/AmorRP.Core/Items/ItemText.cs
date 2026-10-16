
namespace AmorRP.Core.Items;

public static class ItemText
{
    public static bool Plain(string? text, int maximum, bool multiline = false, bool required = true)
        => PlainText.Error(text, maximum, multiline, required) == null;
    public static bool Chat(string? text, int maximum = LiteralChatText.DefaultCharacters) => LiteralChatText.Error(text, maximum) == null;
    public static bool Destination(string text) => text is "emote" or "say" or "party"
        || Enumerable.Range(1, 8).Any(n => text == "linkshell:" + n || text == "crossworld-linkshell:" + n);
}
