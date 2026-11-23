using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<GatheringSettings> gatheringRefresh = new(TimeSpan.FromSeconds(20));
    private GatheringSettings? gathering;
    private int gatherQuantity = 1, gatherWeeklyPoints, alchemySupplyCost = 1, alchemySupplyBonus = 1, cookingSupplyCost = 1, cookingSupplyBonus = 1;
    private GatheringSettingsEdit? savedGatheringDraft;
    private bool configureSupplies;
    private GatheringSettingsEdit GatheringDraft => new(savedGatheringDraft!.ExpectedVersion, gatherWeeklyPoints,
        alchemySupplyCost, alchemySupplyBonus, cookingSupplyCost, cookingSupplyBonus);
    private bool GatheringEdited => savedGatheringDraft != null && (configureSupplies || savedGatheringDraft != GatheringDraft);
    private string gatheringFeedback = "";
    private bool CanGather => Owner || community?.MyCapabilities.Contains(CraftingTypes.GatherPermission) == true;
    private string GatheringPath => $"api/v2/seasons/{community!.Id}/gathering";

    private void ClearGathering()
    {
        gatheringRefresh.Reset();
        gathering = null;
        savedGatheringDraft = null;
        configureSupplies = false;
        gatherQuantity = 1;
        gatheringFeedback = "";
    }

    private void ReadGathering()
    {
        var now = DateTimeOffset.UtcNow;
        if (gatheringRefresh.TryCollect(now, dataGeneration, out var result))
        {
            gathering = result;
            if (!GatheringEdited) LoadGatheringDraft(result);
        }
        if (origin is { } uri && session is { } actor && !SessionRenewalInProgress)
        {
            var path = GatheringPath;
            gatheringRefresh.Start(now, dataGeneration, requests.Token,
                ct => client.GetAsync<GatheringSettings>(uri, path, actor.AccessToken, ct));
        }
    }

    private void DrawGathering()
    {
        ReadGathering();
        DrawRefreshState(gatheringRefresh, "Gathering");
        if (gatheringFeedback.Length > 0) Wrapped(gatheringFeedback);
        if (gathering == null) return;
        if (gathering.Packages.Length == 0)
        {
            Wrapped("The season owner can configure supply packages in Season Management → Weekly limits.");
            return;
        }
        var allowance = quotas?.Items.FirstOrDefault(x => x.Kind == "gathering_points");
        if (allowance == null) return;
        Wrapped($"Gathering and Hunting points: {allowance.Remaining:N0} / {allowance.Limit:N0}");
        ImGui.InputInt("Quantity", ref gatherQuantity);
        foreach (var pack in gathering.Packages)
        {
            ImGui.PushID(pack.TypeId);
            var total = (long)gatherQuantity * pack.Cost;
            Wrapped($"{pack.Name} · {pack.Cost} gathering points each · grants {pack.BonusPoints} crafting points");
            var valid = gatherQuantity > 0 && gatherQuantity <= (serviceLimits?.MaxCreationQuantity ?? 100) && total <= allowance.Remaining;
            ImGui.BeginDisabled(!valid);
            if (ActionButton("Gather " + pack.Name))
                Mutate("POST", GatheringPath + "/packages", new GatherSupplies(pack.TypeId, pack.Revision, gatherQuantity));
            ImGui.EndDisabled();
            ImGui.PopID();
        }
    }

    private void LoadGatheringDraft(GatheringSettings settings)
    {
        gatherWeeklyPoints = settings.NextWeeklyPoints;
        var alchemy = settings.Packages.FirstOrDefault(pack => pack.TypeId == CraftingTypes.AlchemySupplies);
        var cooking = settings.Packages.FirstOrDefault(pack => pack.TypeId == CraftingTypes.CookingSupplies);
        alchemySupplyCost = alchemy?.Cost ?? 1;
        alchemySupplyBonus = alchemy?.BonusPoints ?? 1;
        cookingSupplyCost = cooking?.Cost ?? 1;
        cookingSupplyBonus = cooking?.BonusPoints ?? 1;
        savedGatheringDraft = new(settings.Version, gatherWeeklyPoints, alchemySupplyCost, alchemySupplyBonus, cookingSupplyCost, cookingSupplyBonus);
        configureSupplies = false;
    }

    private bool ValidGatheringDraft(int max) => gatherWeeklyPoints >= 0 && gatherWeeklyPoints <= max &&
        new[] { alchemySupplyCost, alchemySupplyBonus, cookingSupplyCost, cookingSupplyBonus }.All(x => x > 0 && x <= max);

    private void DrawGatheringSettings()
    {
        DrawRefreshState(gatheringRefresh, "Gathering");
        if (gathering == null) return;
        ImGui.TextDisabled($"Active limit: {gathering.WeeklyPoints:N0}");
        ImGui.InputInt("Weekly points per character", ref gatherWeeklyPoints);
        if (gathering.Packages.Length == 0)
            ImGui.Checkbox("Configure supply packages", ref configureSupplies);
        ImGui.InputInt("Alchemy Supplies cost", ref alchemySupplyCost);
        ImGui.InputInt("Alchemy Supplies bonus", ref alchemySupplyBonus);
        ImGui.InputInt("Cooking Supplies cost", ref cookingSupplyCost);
        ImGui.InputInt("Cooking Supplies bonus", ref cookingSupplyBonus);
        ImGui.TextDisabled("Supply costs and bonuses apply to new packages immediately.");
    }

    private void DrawSupplyUse(HoldingDetails detail)
    {
        var alchemy = detail.Holding.TypeId == CraftingTypes.AlchemySupplies;
        Wrapped($"Adds {detail.Definition!.BonusPoints} {(alchemy ? "Alchemy" : "Cooking")} points until the weekly reset.");
        var allowed = alchemy ? CanCraftPotions : CanCraftCooking;
        if (!allowed) Wrapped("You need crafting permission for this discipline to use its supplies.");
        ImGui.BeginDisabled(!allowed || detail.Holding.Available == 0);
        if (ActionButton("Use one supply pack"))
            Mutate("POST", $"api/v2/seasons/{community!.Id}/inventory/{detail.Holding.Id}/use-supplies", new UseSupplies(detail.Holding.Version));
        ImGui.EndDisabled();
    }

    private void ApplyGatheringSuccess(CommandRequest request, string json)
    {
        if (request.Operation.Kind == CommandKind.SaveGatheringSettings)
        {
            gathering = ReadCommand<GatheringSettings>(json);
            LoadGatheringDraft(gathering);
            gatheringFeedback = "Gathering settings saved.";
        }
        else
        {
            var result = ReadCommand<ProductionResult>(json);
            if (quotas != null)
                quotas = quotas with { Items = quotas.Items.Where(x => x.Kind != result.Quota.Kind).Append(result.Quota).ToArray() };
            var use = request.Operation.Kind == CommandKind.UseSupplies;
            gatheringFeedback = use ? "" : $"Received {result.Holding.Name}.";
            if (details?.Holding.Id == result.Holding.Id) details = details with { Holding = result.Holding };
            actionResult = use ? $"Supply pack used. {result.Quota.Remaining:N0} crafting points available." : gatheringFeedback;
        }
        gatheringRefresh.Invalidate();
    }
}
