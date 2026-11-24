using System.Text.Json;
using AmorRP.Contracts.Combat;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Seasons;
using AmorRP.Core.Combat;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<CombatSheet> combatRefresh = new(TimeSpan.FromSeconds(3));
    private readonly BackgroundRefresh<Holding[]> equipmentRefresh = new(TimeSpan.FromSeconds(15));
    private readonly BackgroundRefresh<CombatPolicy> combatPolicyRefresh = new(TimeSpan.FromSeconds(30));
    private CombatSheet? combatSheet;
    private CombatPolicy? combatPolicy, combatDraft;
    private Holding[] equipment = [];
    private readonly int[] allocation = new int[6];
    private string combatFeedback = "";
    private int staffHp = CombatRules.MaxHitPoints;
    public Action<bool>? SetHealthHudVisible { get; set; }
    public Action<bool>? SetActionsHudVisible { get; set; }

    private string CombatPath => $"api/v2/seasons/{community!.Id}/combat";

    private void ResetCombat()
    {
        combatRefresh.Reset();
        equipmentRefresh.Reset();
        combatPolicyRefresh.Reset();
        resetPotionRefresh.Reset();
        combatSheet = null;
        combatPolicy = combatDraft = null;
        resetPotion = null;
        equipment = [];
        Array.Clear(allocation);
        ResetActionsTarget();
        combatFeedback = "";
    }

    private void TickCombat()
    {
        var now = DateTimeOffset.UtcNow;
        if (combatRefresh.TryCollect(now, dataGeneration, out var sheet))
        {
            if (combatSheet != null && !sheet.Scores.SequenceEqual(combatSheet.Scores)) Array.Clear(allocation);
            combatSheet = sheet;
        }
        if (origin is not { } uri || session is not { } actor || community == null || actor.AccessExpiresAt <= now || !serviceCompatible)
            return;
        var path = CombatPath;
        combatRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<CombatSheet>(uri, path, actor.AccessToken, ct));
    }

    private void DrawCharacterStats()
    {
        if (!ImGui.CollapsingHeader("Stats", ImGuiTreeNodeFlags.DefaultOpen)) return;
        if (combatSheet is not { } sheet)
        {
            DrawRefreshState(combatRefresh, "Combat character");
            return;
        }
        ImGui.TextUnformatted($"Armor class {sheet.ArmorClass}");
        DrawCombatStats(sheet);
    }

    private void DrawCharacterEquipment()
    {
        if (combatSheet is not { } sheet) return;
        if (ImGui.CollapsingHeader("Equipment")) DrawEquipment(sheet);
        if (sheet.ResetPotions > 0)
        {
            ImGui.TextUnformatted($"Character Reset Potions: {sheet.ResetPotions}");
            var canReset = sheet.Scores.Any(score => score > CombatRules.BaseScore) || classState?.Selected.Length > 0;
            ImGui.BeginDisabled(!canReset);
            if (ActionButton("Use Character Reset Potion")) Mutate("POST", CombatPath + "/reset", null);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Refund allocated stats and clear selected classes.");
        }
    }

    private void DrawCombatStats(CombatSheet sheet)
    {
        ImGui.TextUnformatted($"Unspent points: {Math.Max(0, sheet.AvailablePoints - allocation.Sum())}");
        if (sheet.BonusPoints > 0) ImGui.TextDisabled($"Permanent points earned from potions: {sheet.BonusPoints}");
        if (ImGui.BeginTable("CombatStats", 4, ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Ability");
            ImGui.TableSetupColumn("Score");
            ImGui.TableSetupColumn("Modifier");
            ImGui.TableSetupColumn("Allocate");
            ImGui.TableHeadersRow();
            for (var i = 0; i < CombatRules.Abilities.Count; i++)
            {
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(CombatRules.Abilities[i]);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted((sheet.Scores[i] + allocation[i]).ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(Signed(CombatRules.AbilityModifier(sheet.Scores[i] + allocation[i])));
                ImGui.TableNextColumn();
                ImGui.BeginDisabled(SeasonReadOnly || allocation[i] == 0);
                if (ImGui.SmallButton("−")) allocation[i]--;
                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.BeginDisabled(SeasonReadOnly || allocation.Sum() >= sheet.AvailablePoints
                    || sheet.Scores[i] + allocation[i] >= CombatRules.MaximumScore);
                if (ImGui.SmallButton("+")) allocation[i]++;
                ImGui.EndDisabled();
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.BeginDisabled(allocation.Sum() == 0 || allocation.Sum() > sheet.AvailablePoints);
        if (ActionButton("Apply stat points")) Mutate("POST", CombatPath + "/stats", new CombatAllocation(sheet.Version, allocation.ToArray()));
        ImGui.EndDisabled();
    }

    private static string Signed(int n) => n >= 0 ? $"+{n}" : n.ToString();

    private void DrawEquipment(CombatSheet sheet)
    {
        var now = DateTimeOffset.UtcNow;
        if (equipmentRefresh.TryCollect(now, dataGeneration, out var result)) equipment = result;
        var path = $"api/v2/seasons/{community!.Id}/inventory?typeIds=weapon,armor&sort=name";
        var uri = origin!;
        var token = session!.AccessToken;
        equipmentRefresh.Start(now, dataGeneration, requests.Token, async ct =>
        {
            var items = new List<Holding>();
            string? cursor = null;
            do
            {
                var page = await client.GetAsync<HoldingPage>(uri, cursor == null ? path : path + "&cursor=" + Uri.EscapeDataString(cursor), token, ct);
                items.AddRange(page.Items);
                cursor = page.NextCursor;
            } while (cursor != null);
            return items.ToArray();
        });
        DrawRefreshState(equipmentRefresh, "Equipment");
        DrawEquipmentSlot("weapon", sheet.Weapon, sheet.Version);
        DrawEquipmentSlot("armor", sheet.Armor, sheet.Version);
    }

    private void DrawEquipmentSlot(string slot, EquippedItem? worn, int version)
    {
        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
        if (ImGui.BeginCombo(TypeName(slot), worn?.Name ?? "Empty"))
        {
            if (worn != null && ImGui.Selectable("Unequip")) Mutate("PUT", CombatPath + "/equipment", new CombatEquip(version, slot, null));
            foreach (var item in equipment.Where(x => x.TypeId == slot && x.Available > 0))
                if (ImGui.Selectable(item.Name + "###gear" + item.Id, worn?.HoldingId == item.Id) && worn?.HoldingId != item.Id)
                    Mutate("PUT", CombatPath + "/equipment", new CombatEquip(version, slot, item.Id));
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        if (worn != null) ImGui.TextDisabled(GearLabel(worn.Gear));
    }

    private static string GearLabel(CombatGear gear) =>
        gear.Form == "armor" ? $"Armor class {gear.Value}" : $"{gear.Ability} · Power {gear.Value}";

    public CombatSheet[] HealthHudRows() => combatSheet is { } sheet && community?.SeasonState == "active" ? [sheet] : [];
    public void DrawHealthHud(CombatSheet[] sheets)
    {
        if (configuration.HealthHud.ShowHeader) ImGui.TextUnformatted("Health");
        if (sheets.Length > 0) DrawHealthBar(sheets[0]);
    }

    private static void DrawHealthBar(CombatSheet sheet)
    {
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new System.Numerics.Vector4(.64f, .20f, .22f, 1f));
        ImGui.ProgressBar((float)sheet.HitPoints / sheet.MaxHitPoints, new(-1, ImGui.GetTextLineHeight()),
            sheet.HitPoints == 0 ? "Downed" : $"{sheet.HitPoints} / {sheet.MaxHitPoints} HP");
        ImGui.PopStyleColor();
    }

    private void DrawCombatRules()
    {
        var now = DateTimeOffset.UtcNow;
        if (combatPolicyRefresh.TryCollect(now, dataGeneration, out var updated) && combatPolicy?.Version != updated.Version)
            combatPolicy = combatDraft = updated;
        var path = CombatPath + "/policy";
        var token = session!.AccessToken;
        var uri = origin!;
        combatPolicyRefresh.Start(now, dataGeneration, requests.Token, ct => client.GetAsync<CombatPolicy>(uri, path, token, ct));
        DrawRefreshState(combatPolicyRefresh, "Combat rules");
        if (combatDraft is not { } draft) return;
        var points = draft.StatPoints;
        var forge = draft.BlacksmithingPoints;
        if (ImGui.InputInt("Allocatable stat points", ref points)) points = Math.Clamp(points, 0, 60);
        if (ImGui.InputInt("Weekly Blacksmithing points", ref forge)) forge = Math.Clamp(forge, 0, 100000);
        var effect = draft.DownedEffect;
        var valid = DrawMoodleEditor("Downed Moodle", ref effect, ownership: true, persistentDescription: "Displayed while the character has 0 HP.");
        combatDraft = draft with { StatPoints = points, BlacksmithingPoints = forge, DownedEffect = effect };
        ImGui.BeginDisabled(!valid || combatDraft == combatPolicy);
        if (ActionButton("Save combat rules")) Mutate("PUT", path, combatDraft);
        ImGui.EndDisabled();
    }

    private void DrawCombatHealthAdmin(Member target)
    {
        if (!ImGui.CollapsingHeader("Combat health")) return;
        if (ImGui.InputInt("Set HP", ref staffHp)) staffHp = Math.Clamp(staffHp, 0, CombatRules.MaxHitPoints);
        if (ActionButton("Apply HP")) Mutate("PUT", CombatPath + "/health/" + target.Character.Id, new CombatHealthEdit(staffHp));
        ImGui.SameLine();
        if (ActionButton("Restore full HP"))
            Mutate("PUT", CombatPath + "/health/" + target.Character.Id, new CombatHealthEdit(CombatRules.MaxHitPoints));
    }

    private void ApplyCombatSuccess(CommandRequest request, CommandResponse response, bool recovering)
    {
        switch (request.Operation.Kind)
        {
            case CommandKind.RollStatCheck:
                var roll = ReadCommand<CombatRoll>(response.Json);
                var check = JsonSerializer.Deserialize<CombatCheck>(request.Json!, FoundationClient.Json)!;
                WriteCombatResult($"{session!.Character.DisplayName}: {roll.Ability} check {roll.Die} {Signed(roll.Modifier)} = {roll.Total}.",
                    check.Destination, recovering || response.Replayed);
                break;
            case CommandKind.AttackCharacter:
                var hit = ReadCommand<CombatHit>(response.Json);
                var attack = JsonSerializer.Deserialize<CombatAttack>(request.Json!, FoundationClient.Json)!;
                var result = hit.Hit ? $"{(hit.Critical ? "Critical hit" : "Hit")} for {hit.Damage} damage{(hit.Downed ? ", Downed" : "")}" : "Miss";
                WriteCombatResult($"{hit.Attacker} attacks {hit.Target}: {hit.Roll.Ability} {hit.Roll.Die} {Signed(hit.Roll.Modifier)} +{hit.Roll.Proficiency} = {hit.Roll.Total} vs AC {hit.ArmorClass}. {result}.",
                    attack.Destination, recovering || response.Replayed);
                break;
            case CommandKind.EditCombatPolicy:
                combatPolicy = combatDraft = ReadCommand<CombatPolicy>(response.Json);
                break;
            case CommandKind.BuyResetPotion:
            case CommandKind.EditResetPotion:
                AcceptResetPotion(ReadCommand<ResetPotionProduct>(response.Json));
                break;
            case CommandKind.UseResetPotion:
                classRefresh.Invalidate();
                resetPotionRefresh.Invalidate();
                goto case CommandKind.AllocateCombatStats;
            case CommandKind.AllocateCombatStats:
                Array.Clear(allocation);
                goto case CommandKind.EquipCombatGear;
            case CommandKind.EquipCombatGear:
                combatSheet = ReadCommand<CombatSheet>(response.Json);
                break;
        }
        combatRefresh.Invalidate();
        moodleRefresh.Invalidate();
    }

    private void WriteCombatResult(string text, string channel, bool replay)
    {
        combatFeedback = text;
        var selected = DestinationIndex(channel);
        if (!replay && selected >= 0)
        {
            var posted = ChatDestination.Available(player, party, selected) && GameChatSender.Send(text, selected, ChatLimit);
            if (!posted) combatFeedback += " Chat channel unavailable.";
        }
        actionResult = combatFeedback;
    }
}
