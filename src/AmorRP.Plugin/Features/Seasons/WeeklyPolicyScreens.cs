using AmorRP.Contracts.Seasons;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private PolicyValues? scheduledPolicyDraft;

    private bool WeeklyLimitsEdited => scheduledPolicyDraft != new PolicyValues(nextPoints, nextLetters, nextCooking);

    private void LoadScheduledPolicy(SeasonPolicies current)
    {
        policyDraftVersion = current.Version;
        scheduledPolicyDraft = current.Next;
        nextPoints = current.Next.PotionPoints;
        nextLetters = current.Next.Letters;
        nextCooking = current.Next.CookingPoints;
    }

    private void AcceptPolicySave(SeasonPolicies updated)
    {
        policies = updated;
        LoadScheduledPolicy(updated);
        if (updated.Gathering is { } settings)
        {
            gathering = settings;
            LoadGatheringDraft(settings);
            gatheringRefresh.Invalidate();
        }
    }

    private void DrawWeeklyPolicy()
    {
        ReadGathering();
        ReadCombatPolicy();
        var maxPoints = serviceLimits?.MaxWeeklyPotionPoints ?? 1000000;
        var maxLetters = serviceLimits?.MaxWeeklyLetters ?? 10000;
        var gatheringEdit = GatheringEdited ? GatheringDraft : null;
        var stale = policies != null && policyDraftVersion != policies.Version ||
            gatheringEdit != null && gathering?.Version != gatheringEdit.ExpectedVersion;
        var valid = nextPoints >= 0 && nextPoints <= maxPoints && nextCooking >= 0 && nextCooking <= maxPoints &&
            nextLetters >= 0 && nextLetters <= maxLetters && (gatheringEdit == null || ValidGatheringDraft(maxPoints));

        ImGui.BeginDisabled(policies == null || stale || !valid || (!WeeklyLimitsEdited && gatheringEdit == null));
        if (ActionButton("Save weekly limits"))
            Mutate("PUT", $"api/v2/seasons/{community!.Id}/policies", new PolicyEdit(nextPoints, nextLetters, nextCooking, gatheringEdit),
                ETag("policy", community.Id, policyDraftVersion));
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(combatPolicy == null || combatDraft == null || combatPolicy.BlacksmithingPoints == combatDraft.BlacksmithingPoints);
        if (ActionButton("Save Blacksmithing limit") && combatPolicy is { } saved && combatDraft is { } forgeDraft)
            Mutate("PUT", CombatPolicyPath, saved with { BlacksmithingPoints = forgeDraft.BlacksmithingPoints });
        ImGui.EndDisabled();
        if (stale && policies != null)
        {
            ImGui.SameLine();
            if (ActionButton("Reload saved settings", mutation: false))
            {
                LoadScheduledPolicy(policies);
                if (gathering != null) LoadGatheringDraft(gathering);
            }
            Wrapped("The saved settings changed while you were editing.");
        }
        if (!valid) Wrapped("Enter weekly limits within range and positive supply costs and bonuses.");

        DrawRefreshState(policyRefresh, "Weekly policy");
        if (policies == null) return;
        ImGui.TextUnformatted($"Weekly limits reset on {policies.NextResetAt.LocalDateTime:g}.");
        ImGui.BeginDisabled(WriteBlocked || SeasonReadOnly);
        DrawWeeklyLimitField("Alchemy", "Points per character", "##WeeklyAlchemyPoints", policies.Current.PotionPoints, ref nextPoints);
        DrawWeeklyLimitField("Cooking", "Points per character", "##WeeklyCookingPoints", policies.Current.CookingPoints, ref nextCooking);
        DrawBlacksmithingLimit();
        DrawWeeklyLimitField("Writing", "Books, letters and notes per character", "##WeeklyWrittenItems", policies.Current.Letters, ref nextLetters);
        ImGui.Spacing();
        DrawSectionHeading("Gathering and Hunting");
        DrawGatheringSettings();
        ImGui.EndDisabled();
    }

    private void DrawBlacksmithingLimit()
    {
        ImGui.Spacing();
        DrawSectionHeading("Blacksmithing");
        DrawRefreshState(combatPolicyRefresh, "Blacksmithing limit");
        if (combatDraft is not { } draft) return;

        FieldLabel("Points per character");
        var points = draft.BlacksmithingPoints;
        if (ImGui.InputInt("##WeeklyBlacksmithingPoints", ref points))
            combatDraft = draft with { BlacksmithingPoints = Math.Clamp(points, 0, 100000) };
        ImGui.TextDisabled("The new limit applies when a character next starts using their weekly points.");
    }

    private static void DrawWeeklyLimitField(string section, string label, string id, int current, ref int scheduled)
    {
        ImGui.Spacing();
        DrawSectionHeading(section);
        ImGui.TextDisabled($"Active limit: {current:N0}");
        FieldLabel(label);
        ImGui.InputInt(id, ref scheduled);
    }
}
