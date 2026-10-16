using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Services.Game;

public sealed record CharacterTarget(string Name, uint HomeWorldId);

public sealed class CharacterContext : IDisposable
{
    private readonly IContextMenu menus;
    private readonly Action<CharacterTarget> selected;

    public CharacterContext(IContextMenu menus, Action<CharacterTarget> selected)
    {
        this.menus = menus;
        this.selected = selected;
        menus.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (args.MenuType != ContextMenuType.Default || args.Target is not MenuTargetDefault target) return;
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
            Name = "Amor RP: Trade",
            OnClicked = _ => selected(snapshot)
        });
    }

    public void Dispose() => menus.OnMenuOpened -= OnMenuOpened;
}
