using AmorRP.Contracts.Communities;
using AmorRP.Plugin.Services.Api;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private bool policyPointsDirty, policyLettersDirty, policyCookingDirty;
    private int activePolicyPoints, activePolicyLetters, activePolicyCooking;
    private void LoadActivePolicy(CommunityPolicies current)
    {
        policyDraftLoaded = true; policyDraftVersion = current.Version;
        nextPoints = activePolicyPoints = current.Current.PotionPoints;
        nextLetters = activePolicyLetters = current.Current.Letters;
        nextCooking = activePolicyCooking = current.Current.CookingPoints;
        policyPointsDirty = policyLettersDirty = policyCookingDirty = false;
    }
    private void AcceptPolicySave(CommandRequest request, CommunityPolicies updated)
    {
        policies = updated; policyDraftVersion = updated.Version;
        var sent = JsonSerializer.Deserialize<PolicyEdit>(request.Json!, FoundationClient.Json)!;
        if (sent.PotionPoints == nextPoints) policyPointsDirty = false;
        if (sent.Letters == nextLetters) policyLettersDirty = false;
        if (sent.CookingPoints == nextCooking) policyCookingDirty = false;
    }
    private void DrawWeeklyPolicy()
    {
        if (!ImGui.CollapsingHeader("Weekly Policy")) return;
        DrawRefreshState(policyRefresh, "Weekly policy");
        if (policies == null) return;
        if (!policyDraftLoaded) LoadActivePolicy(policies);
        if (activePolicyPoints != policies.Current.PotionPoints && !policyPointsDirty) nextPoints = policies.Current.PotionPoints;
        if (activePolicyLetters != policies.Current.Letters && !policyLettersDirty) nextLetters = policies.Current.Letters;
        if (activePolicyCooking != policies.Current.CookingPoints && !policyCookingDirty) nextCooking = policies.Current.CookingPoints;
        activePolicyPoints = policies.Current.PotionPoints; activePolicyLetters = policies.Current.Letters; activePolicyCooking = policies.Current.CookingPoints;
        var stale = policyDraftVersion != policies.Version;
        if (stale) Wrapped("The policy changed. Reload active settings before saving.");
        if (ActionButton("Reload active settings", mutation: false)) LoadActivePolicy(policies);
        Wrapped("Changes take effect at the next weekly reset: " + policies.NextResetAt.LocalDateTime.ToString("g"));
        if (!ImGui.BeginTabBar("WeeklyPolicyTypes")) return;
        if (ImGui.BeginTabItem("Alchemy"))
        {
            Wrapped($"Active: {policies.Current.PotionPoints} points per character. Scheduled: {policies.Next.PotionPoints}.");
            FieldLabel("Alchemy points per character / week"); policyPointsDirty |= ImGui.InputInt("##WeeklyAlchemyPoints", ref nextPoints);
            ImGui.BeginDisabled(stale || nextPoints < 0 || nextPoints > (serviceLimits?.MaxWeeklyPotionPoints ?? 1000000));
            if (ActionButton("Schedule Alchemy points")) SaveWeeklyPolicy(new(nextPoints, policies.Next.Letters, policies.Next.CookingPoints));
            ImGui.EndDisabled(); ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Cooking"))
        {
            Wrapped($"Active: {policies.Current.CookingPoints} points per character. Scheduled: {policies.Next.CookingPoints}.");
            FieldLabel("Cooking points per character / week"); policyCookingDirty |= ImGui.InputInt("##WeeklyCookingPoints", ref nextCooking);
            ImGui.BeginDisabled(stale || nextCooking < 0 || nextCooking > (serviceLimits?.MaxWeeklyPotionPoints ?? 1000000));
            if (ActionButton("Schedule Cooking points")) SaveWeeklyPolicy(new(policies.Next.PotionPoints, policies.Next.Letters, nextCooking));
            ImGui.EndDisabled(); ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Writing"))
        {
            Wrapped($"Active: {policies.Current.Letters} written items per character. Scheduled: {policies.Next.Letters}.");
            FieldLabel("Books, letters and notes per character / week"); policyLettersDirty |= ImGui.InputInt("##WeeklyWrittenItems", ref nextLetters);
            ImGui.BeginDisabled(stale || nextLetters < 0 || nextLetters > (serviceLimits?.MaxWeeklyLetters ?? 10000));
            if (ActionButton("Schedule writing limit")) SaveWeeklyPolicy(new(policies.Next.PotionPoints, nextLetters, policies.Next.CookingPoints));
            ImGui.EndDisabled(); ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }
    private void SaveWeeklyPolicy(PolicyEdit edit) => Mutate("PUT", $"api/v2/communities/{community!.Id}/policies", edit, ETag("policy", community.Id, policyDraftVersion));
}
