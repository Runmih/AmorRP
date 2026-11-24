using System.Globalization;
using AmorRP.Contracts.Combat;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<ResetPotionProduct> resetPotionRefresh = new(TimeSpan.FromSeconds(15));
    private ResetPotionProduct? resetPotion;
    private string resetPrice = "";
    private int resetLimit;

    private string ResetPotionPath => ShopPath + "/reset-potion";

    private void AcceptResetPotion(ResetPotionProduct product)
    {
        if (resetPotion?.Version != product.Version)
        {
            resetPrice = product.Price.ToString(CultureInfo.InvariantCulture);
            resetLimit = product.PerCharacterLimit;
        }
        resetPotion = product;
    }

    private void DrawResetPotion(bool manage)
    {
        var now = DateTimeOffset.UtcNow;
        if (resetPotionRefresh.TryCollect(now, dataGeneration, out var listing)) AcceptResetPotion(listing);
        var uri = origin!;
        var token = session!.AccessToken;
        var path = ResetPotionPath;
        resetPotionRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<ResetPotionProduct>(uri, path, token, ct));
        DrawRefreshState(resetPotionRefresh, "Character Reset Potion");
        if (resetPotion is not { } product) return;
        if (!ImGui.CollapsingHeader("Character Reset Potion", ImGuiTreeNodeFlags.DefaultOpen)) return;
        if (manage)
        {
            ImGui.InputText("Price", ref resetPrice, 21, ImGuiInputTextFlags.CharsDecimal);
            if (ImGui.InputInt("Purchases per character this season", ref resetLimit)) resetLimit = Math.Clamp(resetLimit, 0, 1000);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Set 0 for unlimited purchases.");
            var valid = long.TryParse(resetPrice, NumberStyles.None, CultureInfo.InvariantCulture, out var price) && price > 0;
            ImGui.BeginDisabled(!valid || (price == product.Price && resetLimit == product.PerCharacterLimit));
            if (ActionButton("Save reset potion settings"))
                Mutate("PUT", path, new ResetPotionSettings(product.Version, price, resetLimit));
            ImGui.EndDisabled();
        }
        else
        {
            Wrapped("Refunds allocated stat points and clears chosen classes. Use it from Home → Character.");
            ImGui.TextUnformatted($"Price: {product.Price:N0} {community!.Currency.Name} · Owned: {product.Owned}");
            if (product.PerCharacterLimit > 0)
                ImGui.TextUnformatted($"Purchased this season: {product.Purchased} / {product.PerCharacterLimit}");
            var atLimit = product.PerCharacterLimit > 0 && product.Purchased >= product.PerCharacterLimit;
            ImGui.BeginDisabled(atLimit);
            if (ActionButton("Buy Character Reset Potion")) Mutate("POST", path + "/purchases", new ResetPotionBuy(product.Version));
            ImGui.EndDisabled();
        }
        ImGui.Separator();
    }
}
