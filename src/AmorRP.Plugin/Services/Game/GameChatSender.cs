using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AmorRP.Plugin.Services.Game;

public static class GameChatSender
{
    // Called only on Dalamud's UI thread only for an explicit Use or manual resend action.
    public static unsafe bool Send(string text, int destination, int maximum = AmorRP.Core.Items.LiteralChatText.DefaultCharacters)
    {
        if (!ChatCommand.TryCreate(text, destination, out var command, maximum)) return false;
        var module = UIModule.Instance();
        if (module == null) return false;
        var native = Utf8String.FromString(command);
        if (native == null) return false;
        try { module->ProcessChatBoxEntry(native); }
        finally { native->Dtor(true); }
        return true;
    }
}
