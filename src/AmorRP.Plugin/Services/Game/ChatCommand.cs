using AmorRP.Core.Items;

namespace AmorRP.Plugin.Services.Game;

public static class ChatCommand
{
    public static readonly string[] Destinations = ["Emote", "Say", "Party",
        "Linkshell 1", "Linkshell 2", "Linkshell 3", "Linkshell 4", "Linkshell 5", "Linkshell 6", "Linkshell 7", "Linkshell 8",
        "Cross-world linkshell 1", "Cross-world linkshell 2", "Cross-world linkshell 3", "Cross-world linkshell 4",
        "Cross-world linkshell 5", "Cross-world linkshell 6", "Cross-world linkshell 7", "Cross-world linkshell 8"];

    public static bool TryCreate(string text, int destination, out string command, int maximum = LiteralChatText.DefaultCharacters)
    {
        command = "";
        if (destination < 0 || destination >= Destinations.Length || LiteralChatText.Error(text, maximum) != null) return false;
        var prefix = destination switch
        {
            0 => "/em", 1 => "/say", 2 => "/p",
            >= 3 and <= 10 => $"/l{destination - 2}",
            _ => $"/cwl{destination - 10}"
        };
        command = prefix + " " + text;
        return true;
    }
}
