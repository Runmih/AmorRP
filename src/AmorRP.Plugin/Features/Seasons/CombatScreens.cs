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
    private int staffHp = CombatRules.StartingHitPoints;
    public Action<bool>? SetHealthHudVisible { get; set; }
    public Action<bool>? SetActionsHudVisible { get; set; }

    private string CombatPath => $"api/v2/seasons/{community!.Id}/combat";

    private void ResetCombat()
    {
        combatRefresh.Reset();
        equipmentRefresh.Reset();
        combatPolicyRefresh.Reset();
        ResetSkillLibrary();
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
        var draftCost = sheet.Scores.Select((score, index) => CombatRules.AllocationCost(score, allocation[index])).Sum();
        ImGui.TextUnformatted($"Unspent points: {Math.Max(0, sheet.AvailablePoints - draftCost)}");
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
                ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly || allocation[i] == 0);
                if (ImGui.SmallButton("-")) allocation[i]--;
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Undo one pending increase. Saved scores reset with a Character Reset Potion.");
                ImGui.EndDisabled();
                ImGui.SameLine();
                var previewScore = sheet.Scores[i] + allocation[i];
                var nextCost = CombatRules.AllocationCost(previewScore, 1);
                ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly || draftCost + nextCost > sheet.AvailablePoints
                    || previewScore >= CombatRules.PointBuyMaximum);
                if (ImGui.SmallButton("+")) allocation[i]++;
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(previewScore >= CombatRules.PointBuyMaximum
                        ? "Point buy caps new increases at 15." : $"Cost: {nextCost} point(s).");
                ImGui.EndDisabled();
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.BeginDisabled(draftCost == 0 || draftCost > sheet.AvailablePoints);
        if (ActionButton("Apply stat points")) Mutate("POST", CombatPath + "/stats", new CombatAllocation(sheet.Version, allocation.ToArray()));
        ImGui.EndDisabled();
    }

    private static string StatusRollText(CombatRoll roll) =>
        roll.StatusModifier == 0 ? "" : $" {Signed(roll.StatusModifier)} status";

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
        gear.Form == "armor" ? $"Armor class {gear.Value}" : $"{gear.Ability} · {gear.DiceCount}d{gear.DiceSides}";

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

    private string CombatPolicyPath => CombatPath + "/policy";

    private void ReadCombatPolicy()
    {
        var now = DateTimeOffset.UtcNow;
        if (combatPolicyRefresh.TryCollect(now, dataGeneration, out var updated) && combatPolicy?.Version != updated.Version)
            combatPolicy = combatDraft = updated;
        var path = CombatPolicyPath;
        var token = session!.AccessToken;
        var uri = origin!;
        combatPolicyRefresh.Start(now, dataGeneration, requests.Token, ct => client.GetAsync<CombatPolicy>(uri, path, token, ct));
    }

    private void DrawStatPointSettings()
    {
        ReadCombatPolicy();
        DrawSectionHeading("Stat points");
        DrawRefreshState(combatPolicyRefresh, "Stat points");
        if (combatPolicy is not { } saved || combatDraft is not { } draft) return;
        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
        var points = draft.StatPoints;
        if (ImGui.InputInt("Point-buy budget", ref points)) points = Math.Clamp(points, 0, 60);
        combatDraft = draft with { StatPoints = points };
        ImGui.BeginDisabled(points == saved.StatPoints);
        if (ActionButton("Save stat points")) Mutate("PUT", CombatPolicyPath, saved with { StatPoints = points });
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }

    private void DrawDownedMoodleSettings()
    {
        ReadCombatPolicy();
        DrawRefreshState(combatPolicyRefresh, "Downed Moodle");
        if (combatPolicy is not { } saved || combatDraft is not { } draft) return;
        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
        var effect = draft.DownedEffect;
        var valid = DrawMoodleEditor("Downed", ref effect, ownership: true, persistentDescription: "Displayed while the character has 0 HP.");
        combatDraft = draft with { DownedEffect = effect };
        ImGui.BeginDisabled(!valid || effect == saved.DownedEffect);
        if (ActionButton("Save Downed Moodle")) Mutate("PUT", CombatPolicyPath, saved with { DownedEffect = effect });
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }

    private void AcceptCombatPolicySave(CombatPolicy updated, CombatPolicy submitted)
    {
        var draft = combatDraft ?? submitted;
        combatPolicy = updated;
        combatDraft = updated with
        {
            StatPoints = draft.StatPoints == submitted.StatPoints ? updated.StatPoints : draft.StatPoints,
            DownedEffect = draft.DownedEffect == submitted.DownedEffect ? updated.DownedEffect : draft.DownedEffect,
            BlacksmithingPoints = draft.BlacksmithingPoints == submitted.BlacksmithingPoints
                ? updated.BlacksmithingPoints : draft.BlacksmithingPoints
        };
        combatPolicyRefresh.Invalidate();
    }

    private void DrawCombatHealthAdmin(Member target)
    {
        if (!ImGui.CollapsingHeader("Combat health")) return;
        if (ImGui.InputInt("Set HP", ref staffHp)) staffHp = Math.Clamp(staffHp, 0, CombatRules.HighestHitPoints);
        if (ActionButton("Apply HP")) Mutate("PUT", CombatPath + "/health/" + target.Character.Id, new CombatHealthEdit(staffHp));
        ImGui.SameLine();
        if (ActionButton("Restore full HP"))
            Mutate("PUT", CombatPath + "/health/" + target.Character.Id, new CombatHealthEdit(0, RestoreFull: true));
    }

    private void ApplyCombatSuccess(CommandRequest request, CommandResponse response, bool recovering)
    {
        switch (request.Operation.Kind)
        {
            case CommandKind.RollStatCheck:
                var roll = ReadCommand<CombatRoll>(response.Json);
                var check = JsonSerializer.Deserialize<CombatCheck>(request.Json!, FoundationClient.Json)!;
                WriteCombatResult($"{session!.Character.DisplayName}: {roll.Ability} check {roll.Die} {Signed(roll.Modifier)}{StatusRollText(roll)} = {roll.Total}.",
                    check.Destination, recovering || response.Replayed);
                break;
            case CommandKind.AttackCharacter:
                var hit = ReadCommand<CombatHit>(response.Json);
                var attack = JsonSerializer.Deserialize<CombatAttack>(request.Json!, FoundationClient.Json)!;
                var result = hit.Hit ? $"{(hit.Critical ? "Critical hit" : "Hit")} for {hit.Damage} damage{(hit.Downed ? ", Downed" : "")}" : "Miss";
                if (hit.StayedStanding) result += ", target stays standing at 1 HP";
                if (hit.Hit && hit.DamageDiceCount > 0)
                    result += $" ({hit.DamageDiceCount}d{hit.DamageDiceSides}: {hit.RolledDamage} {Signed(hit.Roll.Modifier)})";
                WriteCombatResult($"{hit.Attacker} attacks {hit.Target}: {hit.Roll.Ability} {hit.Roll.Die} {Signed(hit.Roll.Modifier)} +{hit.Roll.Proficiency}{StatusRollText(hit.Roll)} = {hit.Roll.Total} vs AC {hit.ArmorClass}. {result}.",
                    attack.Destination, recovering || response.Replayed);
                break;
            case CommandKind.SaveStatusEffect:
                AcceptStatusEffect(ReadCommand<StatusEffect>(response.Json));
                break;
            case CommandKind.SaveSkillLibrary:
                AcceptSkillLibrary(ReadCommand<SkillLibrary>(response.Json));
                break;
            case CommandKind.UseSkill:
            {
                var skill = ReadCommand<SkillResult>(response.Json);
                var use = JsonSerializer.Deserialize<SkillUse>(request.Json!, FoundationClient.Json)!;
                var outcome = skill.Effect == "status" ? (skill.AppliedStatus is { } applied ? applied + " applied" : "no effect")
                    : skill.Applied ? $"{skill.Amount} {skill.Effect}" : "no effect";
                var rollDetails = skill.Roll is { } skillRoll ? $" ({skillRoll.Ability} {skillRoll.Total} vs {skill.Difficulty})" : "";
                var standing = skill.StayedStanding ? $" {skill.Target} stays standing at 1 HP." : "";
                if (skill.Effect != "status" && skill.AppliedStatus is { } status) outcome += $", {status} applied";
                WriteCombatResult($"{skill.Actor} uses {skill.Skill} on {skill.Target}: {outcome}{rollDetails}.{standing}",
                    use.Destination, recovering || response.Replayed);
                break;
            }
            case CommandKind.EditCombatPolicy:
                AcceptCombatPolicySave(ReadCommand<CombatPolicy>(response.Json),
                    JsonSerializer.Deserialize<CombatPolicy>(request.Json!, FoundationClient.Json)!);
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
