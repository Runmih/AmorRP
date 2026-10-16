using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace AmorRP.Plugin.Services.Game;

public static class ChatDestination
{
    // Local membership checks only, on the draw thread; never query other players.
    public static unsafe bool Available(IPlayerState player, IPartyList party, int destination)
    {
        if (!player.IsLoaded) return false;
        if (destination is 0 or 1) return true;
        if (destination == 2) return party.Length > 0;
        if (InfoModule.Instance() == null) return false;
        if (destination is >= 3 and <= 10)
        {
            var proxy = InfoProxyLinkshell.Instance();
            return proxy != null && proxy->LinkShells[destination - 3].Id != 0 && proxy->LinkShells[destination - 3].ChatId != 0;
        }
        if (destination is >= 11 and <= 18)
        {
            var proxy = InfoProxyCrossWorldLinkshell.Instance();
            return proxy != null && proxy->CrossWorldLinkshells[destination - 11].MembershipType > 0;
        }
        return false;
    }
}
