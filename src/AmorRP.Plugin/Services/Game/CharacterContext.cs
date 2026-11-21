using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AmorRP.Plugin.Services.Game;

public sealed record CharacterTarget(string Name, uint HomeWorldId);

public sealed class CharacterContext : IDisposable
{
    private const string MenuName = "Amor RP";
    private const SeIconChar Heart = (SeIconChar)'♥';
    private readonly IContextMenu menus;
    private readonly Action<CharacterTarget> trade, bill;
    private readonly Func<bool> canBill;
    private readonly Func<bool> isAvailable;

    public CharacterContext(IContextMenu menus, Action<CharacterTarget> trade, Action<CharacterTarget> bill, Func<bool> isAvailable, Func<bool> canBill)
    {
        this.menus = menus;
        this.trade = trade;
        this.bill = bill;
        this.canBill = canBill;
        this.isAvailable = isAvailable;
        menus.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!isAvailable() || args.MenuType != ContextMenuType.Default || args.Target is not MenuTargetDefault target) return;
        // An NPC can have a name too. Require character evidence; never read/store ContentId.
        var player = target.TargetObject as IPlayerCharacter;
        var character = target.TargetCharacter;
        if (player is null && character is null) return;
        var name = player?.Name.TextValue ?? target.TargetName;
        var world = player?.HomeWorld.RowId ?? target.TargetHomeWorld.RowId;
        if (string.IsNullOrWhiteSpace(name) || world == 0 || name.Length > 80) return;
        var snapshot = new CharacterTarget(name, world);
        args.AddMenuItem(new MenuItem
        {
            Name = MenuName,
            Prefix = Heart,
            PrefixColor = IMenuItem.DalamudDefaultPrefixColor,
            IsSubmenu = true,
            OnClicked = click =>
            {
                if (!isAvailable()) return;
                List<IMenuItem> actions =
                [
                    new MenuItem
                    {
                        Name = "Trade",
                        OnClicked = _ => { if (isAvailable()) trade(snapshot); }
                    }
                ];
                if (canBill())
                    actions.Add(new MenuItem
                    {
                        Name = "Billing",
                        OnClicked = _ => { if (isAvailable() && canBill()) bill(snapshot); }
                    });
                actions.Add(new MenuItem { Name = "Return", Priority = int.MaxValue, IsReturn = true, OnClicked = ReturnToParent });
                click.OpenSubmenu(MenuName, actions);
            }
        });
    }

    private static unsafe void ReturnToParent(IMenuItemClickedArgs click)
    {
        var agent = AgentContext.Instance();
        if (agent == null || click.AgentPtr != (nint)agent) return;
        // Open the parent from the agent's current menu data. Dalamud dismisses the clicked submenu.
        agent->OpenContextMenu(closeExisting: false);
    }

    public void Dispose() => menus.OnMenuOpened -= OnMenuOpened;
}
