using AmorRP.Contracts.Communities;
using AmorRP.Plugin.Services.Api;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private bool policyPointsDirty, policyLettersDirty;
    private int activePolicyPoints, activePolicyLetters;
    private void LoadActivePolicy(CommunityPolicies current)
    {
        policyDraftLoaded = true; policyDraftVersion = current.Version;
        nextPoints = activePolicyPoints = current.Current.PotionPoints;
        nextLetters = activePolicyLetters = current.Current.Letters;
        policyPointsDirty = policyLettersDirty = false;
    }
    private void AcceptPolicySave(CommandRequest request, CommunityPolicies updated)
    {
        policies = updated; policyDraftVersion = updated.Version;
        var sent = JsonSerializer.Deserialize<PolicyEdit>(request.Json!, FoundationClient.Json)!;
        if (sent.PotionPoints == nextPoints) policyPointsDirty = false;
        if (sent.Letters == nextLetters) policyLettersDirty = false;
    }
    private void DrawWeeklyPolicy()
    {
        if (policies == null || !ImGui.CollapsingHeader("Weekly Policy")) return;
        if (!policyDraftLoaded) LoadActivePolicy(policies);
        if (activePolicyPoints != policies.Current.PotionPoints && !policyPointsDirty) nextPoints = policies.Current.PotionPoints;
        if (activePolicyLetters != policies.Current.Letters && !policyLettersDirty) nextLetters = policies.Current.Letters;
        activePolicyPoints = policies.Current.PotionPoints; activePolicyLetters = policies.Current.Letters;
        var stale = policyDraftVersion != policies.Version;
        if (stale) Wrapped("The policy changed. Reload active settings before saving.");
        if (ActionButton("Reload active settings", mutation: false)) LoadActivePolicy(policies);
        Wrapped("Changes take effect at the next weekly reset: " + policies.NextResetAt.LocalDateTime.ToString("g"));
        if (!ImGui.BeginTabBar("WeeklyPolicyTypes")) return;
        if (ImGui.BeginTabItem("Potions"))
        {
            Wrapped($"Active: {policies.Current.PotionPoints} points per character. Scheduled: {policies.Next.PotionPoints}.");
            FieldLabel("Potion points per character / week"); policyPointsDirty |= ImGui.InputInt("##WeeklyPotionPoints", ref nextPoints);
            ImGui.BeginDisabled(stale || nextPoints < 0 || nextPoints > (serviceLimits?.MaxWeeklyPotionPoints ?? 1000000));
            if (ActionButton("Schedule potion allowance")) Mutate("PUT", $"api/v2/communities/{community!.Id}/policies", new PolicyEdit(nextPoints, policies.Next.Letters), ETag("policy", community.Id, policyDraftVersion));
            ImGui.EndDisabled(); ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Books"))
        {
            Wrapped($"Active: {policies.Current.Letters} written items per character. Scheduled: {policies.Next.Letters}.");
            FieldLabel("Books, letters and notes per character / week"); policyLettersDirty |= ImGui.InputInt("##WeeklyWrittenItems", ref nextLetters);
            ImGui.BeginDisabled(stale || nextLetters < 0 || nextLetters > (serviceLimits?.MaxWeeklyLetters ?? 10000));
            if (ActionButton("Schedule written-item allowance")) Mutate("PUT", $"api/v2/communities/{community!.Id}/policies", new PolicyEdit(policies.Next.PotionPoints, nextLetters), ETag("policy", community.Id, policyDraftVersion));
            ImGui.EndDisabled(); ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }
}
