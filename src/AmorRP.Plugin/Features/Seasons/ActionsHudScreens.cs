using System.Globalization;
using System.Numerics;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Seasons;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private const string AttackAction = "Attack";
    private static readonly (string Id, string Label, FontAwesomeIcon Icon)[] CombatButtons =
    [
        (AttackAction, "Attack", FontAwesomeIcon.Khanda),
        ("STR", "Strength", FontAwesomeIcon.FistRaised),
        ("DEX", "Dexterity", FontAwesomeIcon.ShoePrints),
        ("CON", "Constitution", FontAwesomeIcon.Heartbeat),
        ("INT", "Intelligence", FontAwesomeIcon.Brain),
        ("WIS", "Wisdom", FontAwesomeIcon.BookOpen),
        ("CHA", "Charisma", FontAwesomeIcon.Comments)
    ];
    private static readonly string[] DefaultCombatButtonOrder = CombatButtons.Select(button => button.Id).ToArray();

    private readonly BackgroundRefresh<Member?> actionsTargetRefresh = new(TimeSpan.FromMinutes(1));
    private CharacterTarget? actionsTarget;
    private Member? actionsTargetMember;
    private DateTimeOffset actionsTargetLookupAt;

    private void ResetActionsTarget()
    {
        actionsTargetRefresh.Reset();
        actionsTarget = null;
        actionsTargetMember = null;
    }

    private CharacterTarget? ReadActionsTarget()
    {
        if (targets.Target is not IPlayerCharacter target || target.HomeWorld.RowId == 0) return null;
        var name = target.Name.TextValue;
        var world = target.HomeWorld.RowId;
        return name.Length == 0 || (name == player.CharacterName && world == player.HomeWorld.RowId)
            ? null : new CharacterTarget(name, world);
    }

    private Member? ResolveActionsTarget(CharacterTarget? target)
    {
        var now = DateTimeOffset.UtcNow;
        if (target != actionsTarget)
        {
            ResetActionsTarget();
            actionsTarget = target;
            actionsTargetLookupAt = now.AddMilliseconds(250);
        }
        if (target == null || session is not { } actor || community is not { } season || origin is not { } uri)
            return null;

        var world = target.HomeWorldId.ToString(CultureInfo.InvariantCulture);
        var known = members.FirstOrDefault(member => member.Status == "active"
            && member.Character.DisplayName == target.Name && member.Character.HomeWorldId == world);
        if (known != null) return known;

        if (actionsTargetRefresh.TryCollect(now, dataGeneration, out var resolved)) actionsTargetMember = resolved;
        if (now >= actionsTargetLookupAt)
            actionsTargetRefresh.Start(now, dataGeneration, requests.Token,
                ct => FindSeasonCharacterAsync(uri, actor.AccessToken, season.Id, target, ct));
        return actionsTargetRefresh.Error == null ? actionsTargetMember : null;
    }

    public CombatSheet[] ActionsHudRows() =>
        session is { } actor && actor.AccessExpiresAt > DateTimeOffset.UtcNow && serviceCompatible
        && community != null && combatSheet is { } sheet ? [sheet] : [];

    private string[] OrderedCombatButtons() =>
        configuration.ActionsGrid.ButtonOrder.Concat(DefaultCombatButtonOrder)
            .Where(DefaultCombatButtonOrder.Contains).Distinct().ToArray();

    private int CombatGridColumns => Math.Clamp(configuration.ActionsGrid.Columns, 1, CombatButtons.Length);
    private bool CombatButtonVisible(string id) => !configuration.ActionsGrid.HiddenButtons.Contains(id);

    public int ActionsHudFrameRows(CombatSheet[] sheets)
    {
        if (sheets.Length == 0) return 2;
        var count = CombatButtons.Count(button => CombatButtonVisible(button.Id));
        var rows = Math.Max(1, (count + CombatGridColumns - 1) / CombatGridColumns);
        return rows + (combatFeedback.Length > 0 ? 3 : 0);
    }

    public void DrawActionsHud(CombatSheet[] sheets)
    {
        if (configuration.ActionsHud.ShowHeader) DrawSectionHeading("Actions");
        if (sheets.Length == 0)
        {
            if (session == null) ImGui.TextWrapped("Sign in to use combat actions.");
            else if (community == null) ImGui.TextWrapped("Combat actions require a season.");
            else DrawRefreshState(combatRefresh, "Combat actions");
            return;
        }
        var shown = OrderedCombatButtons().Where(CombatButtonVisible).ToArray();
        if (shown.Length == 0) ImGui.TextWrapped("Choose buttons in Settings → Interface → Actions.");
        else DrawCombatGrid(sheets[0], shown);
        if (combatFeedback.Length > 0) Wrapped(combatFeedback);
    }

    private void DrawCombatGrid(CombatSheet sheet, string[] shown)
    {
        var columns = Math.Min(CombatGridColumns, shown.Length);
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var width = Math.Max(1, (ImGui.GetContentRegionAvail().X - (columns - 1) * gap) / columns);
        var size = new Vector2(width, ImGui.GetFrameHeight());
        for (var i = 0; i < shown.Length; i++)
        {
            if (i % columns != 0) ImGui.SameLine();
            var id = shown[i];
            if (id == AttackAction)
            {
                DrawAttackButton(sheet, size);
                continue;
            }
            var button = CombatButtons.First(button => button.Id == id);
            ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
            if (CombatIcon(button.Icon, button.Label + " check", id, size))
                Mutate("POST", CombatPath + "/checks", new CombatCheck(id, CombatDestination()));
            ImGui.EndDisabled();
        }
    }

    private void DrawAttackButton(CombatSheet sheet, Vector2 size)
    {
        var reason = !sheet.CanAttack ? "Attack permission is disabled."
            : sheet.HitPoints == 0 ? "You are downed."
            : sheet.Weapon == null ? "Equip a weapon to attack."
            : SeasonReadOnly ? "Attacks require an active season."
            : WriteBlocked ? "Combat is temporarily unavailable."
            : null;
        Member? member = null;
        if (reason == null)
        {
            var target = ReadActionsTarget();
            member = ResolveActionsTarget(target);
            if (member == null)
                reason = target == null ? "Select another player in game to attack."
                    : actionsTargetRefresh.Error != null ? "Could not verify the selected player."
                    : actionsTargetRefresh.HasLoaded ? "The selected player is not an active season member."
                    : "Checking the selected player…";
        }
        ImGui.BeginDisabled(reason != null);
        var icon = sheet.Weapon?.Gear.Form == "staff" ? FontAwesomeIcon.WandSparkles : FontAwesomeIcon.Khanda;
        if (CombatIcon(icon, reason ?? $"Attack {member!.Character.DisplayName}", AttackAction, size) && member != null)
            Mutate("POST", CombatPath + "/attacks", new CombatAttack(member.Character.Id, CombatDestination()));
        ImGui.EndDisabled();
    }

    private string CombatDestination()
    {
        var channel = DestinationIndex(configuration.ActionsGrid.ChatChannel);
        return channel >= 0 && ChatDestination.Available(player, party, channel) ? DestinationId(channel) : "";
    }

    private static bool CombatIcon(FontAwesomeIcon icon, string tooltip, string id, Vector2 size)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var clicked = ImGui.Button(icon.ToIconString() + "###" + id, size);
        ImGui.PopFont();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(tooltip);
        return clicked;
    }

    private void DrawActionsGridSettings()
    {
        var grid = configuration.ActionsGrid;
        ImGui.PushID("ActionsGrid");
        var destination = DestinationIndex(grid.ChatChannel);
        if (ImGui.BeginCombo("Post rolls to", destination < 0 ? "Activity Log only" : ChatCommand.Destinations[destination]))
        {
            if (ImGui.Selectable("Activity Log only", destination < 0)) { grid.ChatChannel = ""; save(); }
            for (var i = 0; i < ChatCommand.Destinations.Length; i++)
                if (ImGui.Selectable(ChatCommand.Destinations[i], destination == i)) { grid.ChatChannel = DestinationId(i); save(); }
            ImGui.EndCombo();
        }
        if (destination >= 0 && player.IsLoaded && !ChatDestination.Available(player, party, destination))
            ImGui.TextDisabled("This channel is unavailable. Rolls will stay in the Activity Log.");

        var columns = CombatGridColumns;
        if (ImGui.SliderInt("Buttons per row", ref columns, 1, CombatButtons.Length)) grid.Columns = columns;
        if (ImGui.IsItemDeactivatedAfterEdit()) save();

        var order = OrderedCombatButtons();
        for (var i = 0; i < order.Length; i++)
        {
            var id = order[i];
            var button = CombatButtons.First(button => button.Id == id);
            ImGui.PushID(id);
            ImGui.BeginDisabled(i == 0);
            var moveUp = ImGui.ArrowButton("Up", ImGuiDir.Up);
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(i == order.Length - 1);
            var moveDown = ImGui.ArrowButton("Down", ImGuiDir.Down);
            ImGui.EndDisabled();
            ImGui.SameLine();
            var show = CombatButtonVisible(id);
            if (ImGui.Checkbox(button.Label, ref show))
            {
                grid.HiddenButtons = show ? grid.HiddenButtons.Where(hidden => hidden != id).ToArray() : [.. grid.HiddenButtons, id];
                save();
            }
            ImGui.PopID();
            if (!moveUp && !moveDown) continue;
            var other = i + (moveUp ? -1 : 1);
            (order[i], order[other]) = (order[other], order[i]);
            grid.ButtonOrder = order;
            save();
            break;
        }

        if (ImGui.Button("Reset button layout"))
        {
            grid.ButtonOrder = [];
            grid.HiddenButtons = [];
            grid.Columns = ActionsGridSettings.DefaultColumns;
            save();
        }
        ImGui.PopID();
    }
}
