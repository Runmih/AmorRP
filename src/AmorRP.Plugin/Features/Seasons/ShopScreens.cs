using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Shop;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<ShopProducts> shopRefresh = new(TimeSpan.FromSeconds(15));
    private ShopProduct[] shopProducts = [];
    private Guid? shopSelection, shopGroup, shopDefinition;
    private ShopProduct? editingProduct;
    private int shopQuantity = 1, shopStock, shopBuyerLimit = 1, shopBuyer;
    private bool shopLimitedStock, shopLimitedBuyer, shopListed = true;
    private string shopPrice = "", shopReceipt = "";
    private string ShopPath => $"api/v2/seasons/{community!.Id}/shop";

    private void ClearShop()
    {
        shopRefresh.Reset();
        ClearPermits();
        shopProducts = [];
        shopSelection = shopGroup = null;
        shopQuantity = 1;
        shopReceipt = "";
        LoadShopDraft(null);
    }

    private void DrawShop()
    {
        var now = DateTimeOffset.UtcNow;
        if (shopRefresh.TryCollect(now, dataGeneration, out var loaded)) shopProducts = loaded.Products;
        if (origin is { } uri && session is { } actor && !SessionRenewalInProgress)
        {
            var path = ShopPath + "/products";
            shopRefresh.Start(now, dataGeneration, requests.Token,
                ct => client.GetAsync<ShopProducts>(uri, path, actor.AccessToken, ct));
        }
        DrawRefreshState(shopRefresh, "Shop");
        if (shopReceipt.Length > 0) Wrapped(shopReceipt);
        if (!ImGui.BeginTabBar("SeasonShopTabs")) return;
        if (ImGui.BeginTabItem("Products"))
        {
            DrawShopProducts();
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Slave", permitTabRequested ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
        {
            permitTabRequested = false;
            DrawPermits();
            ImGui.EndTabItem();
        }
        if (classState?.Selected.Length > 0 && ImGui.BeginTabItem("Class reset")) { DrawClassSelection(shop: true); ImGui.EndTabItem(); }
        if (Owner && ImGui.BeginTabItem("Manage products"))
        {
            DrawShopEditor();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawShopProducts()
    {
        var listed = shopProducts.Where(x => x.Listed).ToArray();
        if (listed.Length == 0)
        {
            if (shopRefresh.HasLoaded) Wrapped("No products are listed yet.");
            return;
        }

        foreach (var product in listed)
            if (ImGui.Selectable($"{product.Name} · {product.Price} {community!.Currency.Name}###shop{product.Id}", shopSelection == product.Id))
            {
                shopSelection = product.Id;
                shopGroup = null;
                shopQuantity = 1;
                shopReceipt = "";
            }

        if (listed.FirstOrDefault(x => x.Id == shopSelection) is not { } selected) return;
        ImGui.Separator();
        if (selected.Description.Length > 0) Wrapped(selected.Description);
        if (selected.Stock is { } stock) ImGui.TextUnformatted($"Stock: {stock}");
        if (selected.BuyerLimit is { } limit) ImGui.TextUnformatted($"Limit per buyer: {limit}");

        var groups = communityGroups.Where(group => CanGroupAction(group, GroupCapabilities.TreasuryWithdraw))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (selected.Buyer != "character")
        {
            if (ImGui.BeginCombo("Pay from", groups.FirstOrDefault(group => group.Id == shopGroup)?.Name
                ?? (selected.Buyer == "either" ? "Personal balance" : "Choose a group treasury")))
            {
                if (selected.Buyer == "either" && ImGui.Selectable("Personal balance", shopGroup == null)) shopGroup = null;
                foreach (var group in groups)
                    if (ImGui.Selectable(group.Name + "###shopBuyer" + group.Id, shopGroup == group.Id)) shopGroup = group.Id;
                ImGui.EndCombo();
            }
            if (selected.Buyer == "group" && groups.Length == 0) Wrapped("This listing requires permission to spend from a group treasury.");
        }
        ImGui.InputInt("Quantity", ref shopQuantity);
        var max = serviceLimits?.MaxCreationQuantity ?? 100;
        var fromGroup = selected.Buyer != "character" ? shopGroup : null;
        var validBuyer = fromGroup is { } id ? groups.Any(group => group.Id == id) : selected.Buyer != "group";
        var validQuantity = shopQuantity > 0 && shopQuantity <= max && (selected.Stock == null || shopQuantity <= selected.Stock);
        var validPrice = WholeUnits.TryNormalize(selected.Price, out _, out var unitPrice) && unitPrice > 0;
        var total = (Int128)unitPrice * shopQuantity;
        if (validQuantity) Wrapped($"Total: {total} {community!.Currency.Name}");
        if (fromGroup.HasValue) Wrapped("Purchased items go to your character’s inventory.");
        ImGui.BeginDisabled(!validBuyer || !validQuantity || !validPrice || total > long.MaxValue);
        if (ActionButton("Buy"))
        {
            shopReceipt = "";
            Mutate("POST", ShopPath + "/purchases", new ShopBuy(selected.Id, selected.Version, shopQuantity, fromGroup));
        }
        ImGui.EndDisabled();
    }

    private void LoadShopDraft(ShopProduct? product)
    {
        editingProduct = product;
        shopDefinition = product?.DefinitionId;
        shopPrice = product?.Price ?? "";
        shopStock = product?.Stock ?? 0;
        shopBuyerLimit = product?.BuyerLimit ?? 1;
        shopLimitedStock = product?.Stock != null;
        shopLimitedBuyer = product?.BuyerLimit != null;
        shopListed = product?.Listed ?? true;
        shopBuyer = product?.Buyer switch { "group" => 1, "either" => 2, _ => 0 };
    }

    private void DrawShopEditor()
    {
        if (ImGui.Button("New listing")) LoadShopDraft(null);
        ImGui.SameLine();
        if (ImGui.Button("Refresh listings")) shopRefresh.Invalidate();
        foreach (var product in shopProducts)
            if (ImGui.Selectable(product.Name + (product.Listed ? "" : " · hidden") + "###editShop" + product.Id, editingProduct?.Id == product.Id))
                LoadShopDraft(product);
        ImGui.Separator();
        var chosen = definitions.FirstOrDefault(row => row.Id == shopDefinition);
        ImGui.BeginDisabled(editingProduct != null);
        if (ImGui.BeginCombo("Item", chosen?.Name ?? editingProduct?.Name ?? "Choose an item"))
        {
            foreach (var row in definitions.Where(x => !x.Retired && x.GroupId == null && CraftingTypes.IsRecipe(x.TypeId)))
                if (ImGui.Selectable(row.Name + "###shopDefinition" + row.Id, shopDefinition == row.Id)) shopDefinition = row.Id;
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        ImGui.InputText("Price", ref shopPrice, 21, ImGuiInputTextFlags.CharsDecimal);
        ImGui.Combo("Buyer", ref shopBuyer, ["Character", "Group", "Either"], 3);
        ImGui.Checkbox("Limited stock", ref shopLimitedStock);
        if (shopLimitedStock) ImGui.InputInt("Stock", ref shopStock);
        ImGui.Checkbox("Limit purchases per buyer", ref shopLimitedBuyer);
        if (shopLimitedBuyer) ImGui.InputInt("Buyer limit", ref shopBuyerLimit);
        ImGui.Checkbox("Visible in shop", ref shopListed);
        var stale = editingProduct != null && shopProducts.FirstOrDefault(x => x.Id == editingProduct.Id) is { } current
            && current.Etag != editingProduct.Etag;
        if (stale) Wrapped("The listing changed. Select it again before saving.");
        var valid = WholeUnits.TryNormalize(shopPrice, out var price, out var units) && units > 0 && shopDefinition.HasValue
            && (!shopLimitedStock || shopStock >= 0) && (!shopLimitedBuyer || shopBuyerLimit > 0);
        ImGui.BeginDisabled(!valid || stale);
        if (ActionButton("Save listing"))
        {
            var body = new ShopProductWrite(shopDefinition!.Value, price, shopBuyer switch { 1 => "group", 2 => "either", _ => "character" },
                shopLimitedStock ? shopStock : null, shopLimitedBuyer ? shopBuyerLimit : null, shopListed);
            Mutate(editingProduct == null ? "POST" : "PUT", ShopPath + "/products" + (editingProduct == null ? "" : "/" + editingProduct.Id), body, editingProduct?.Etag);
        }
        ImGui.EndDisabled();
    }

    private void ApplyShopSuccess(CommandRequest request, string json)
    {
        if (request.Operation.Kind == CommandKind.BuyShopProduct)
        {
            var purchase = ReadCommand<ShopPurchase>(json);
            shopReceipt = $"Purchased {purchase.Quantity} × {purchase.Name} for {purchase.Total} {community!.Currency.Name}.";
        }
        else
        {
            var product = ReadCommand<ShopProduct>(json);
            shopProducts = shopProducts.Where(x => x.Id != product.Id).Append(product).OrderBy(x => x.Name).ToArray();
            LoadShopDraft(product);
        }
        shopRefresh.Invalidate();
    }
}
