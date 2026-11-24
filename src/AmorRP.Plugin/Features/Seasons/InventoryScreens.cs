using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Seasons;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private Category[] categories = [];
    private Definition[] definitions = [];
    private Holding[] holdings = [], compact = [], memberHoldings = [];
    private Quotas? quotas;
    private HoldingDetails? details;
    private Guid? holdingSelection;
    private string? holdingCursor, compactCursor, memberHoldingCursor;
    private Guid? memberInventoryFor, memberHoldingSelection;
    private string search = "";
    private string appliedInventoryQuery = "sort=name";
    private string filterType = "";
    private int filterCategory, sort;
    private int loadedInventoryTab;
    private Holding? discardTarget;
    private int discardQuantity = 1, removeQuantity = 1;
    private string itemRemovalReason = "";
    private bool removeItemsConfirmed;
    private HashSet<Guid> pinned = [];
    private sealed record InventoryState(Category[] Categories, Definition[] Definitions, HoldingPage Holdings, HoldingPage Compact, Quotas Quotas, HoldingDetails? Details);
    private string InventoryQuery() => appliedInventoryQuery;
    private string BuildInventoryQuery()
    {
        var args = new List<string> { "sort=" + new[] { "name", "type", "category" }[sort] };
        if (search.Length > 0) args.Add("search=" + Uri.EscapeDataString(search));
        if (filterType.Length > 0) args.Add("typeId=" + filterType);
        else if (inventoryTab != 0) args.Add("typeIds=" + string.Join(",", InventoryTypes()));
        if (inventoryTab == 0 && filterCategory > 0 && filterCategory <= categories.Length) args.Add("categoryId=" + categories[filterCategory - 1].Id);
        return string.Join("&", args);
    }
    private async Task<InventoryState> LoadInventoryAsync(Uri uri, string token, Guid id, string query, Guid? requestedSelection, int requestedCount, CancellationToken ct)
    {
        var prefix = $"api/v2/seasons/{id}/";
        var cats = new List<Category>(); string? cursor = null;
        do { var page = await client.GetAsync<CategoryPage>(uri, prefix + "categories" + (cursor == null ? "" : "?cursor=" + Uri.EscapeDataString(cursor)), token, ct); cats.AddRange(page.Items); cursor = page.NextCursor; } while (cursor != null);
        var recipes = new List<Definition>(); cursor = null;
        do { var page = await client.GetAsync<DefinitionPage>(uri, prefix + "item-definitions" + (cursor == null ? "" : "?cursor=" + Uri.EscapeDataString(cursor)), token, ct); recipes.AddRange(page.Items); cursor = page.NextCursor; } while (cursor != null);
        var pageHoldings = await client.GetAsync<HoldingPage>(uri, prefix + "inventory?" + query, token, ct);
        var rows = pageHoldings.Items.ToList();
        // Keep the number of pages the player has opened during a quiet refresh.
        while (rows.Count < requestedCount && pageHoldings.NextCursor != null)
        { var next = await client.GetAsync<HoldingPage>(uri, prefix + "inventory?" + query + "&cursor=" + Uri.EscapeDataString(pageHoldings.NextCursor), token, ct); rows.AddRange(next.Items); pageHoldings = next; }
        pageHoldings = pageHoldings with { Items = rows.ToArray() };
        var potions = await client.GetAsync<HoldingPage>(uri, prefix + "inventory?typeIds=potion,poison,explosive,drug,medicine,meal,drink,dessert,alchemy-supplies,cooking-supplies&sort=name", token, ct);
        var allowance = await client.GetAsync<Quotas>(uri, prefix + "quotas", token, ct);
        HoldingDetails? selectedDetails = null;
        if (requestedSelection.HasValue)
        {
            try { selectedDetails = await client.GetAsync<HoldingDetails>(uri, prefix + "inventory/" + requestedSelection, token, ct); }
            catch (FoundationApiException e) when (e.Status == System.Net.HttpStatusCode.NotFound) { }
        }
        return new(cats.ToArray(), recipes.ToArray(), pageHoldings, potions, allowance, selectedDetails);
    }
    private void AcceptInventoryState(InventoryState? state)
    {
        if (state == null) { ClearInventoryViews(); return; }
        var filterId = filterCategory > 0 && filterCategory <= categories.Length ? (Guid?)categories[filterCategory - 1].Id : null;
        var productionTypes = CraftingTypes.Types(craftDiscipline);
        var currentRecipes = definitions.Where(x => !x.Retired && x.Enabled && productionTypes.Contains(x.TypeId)).ToArray();
        var recipeId = recipeChoice < currentRecipes.Length ? (Guid?)currentRecipes[recipeChoice].Id : null;
        categories = state.Categories; definitions = state.Definitions; holdings = state.Holdings.Items; holdingCursor = state.Holdings.NextCursor;
        compact = state.Compact.Items; compactCursor = state.Compact.NextCursor; quotas = state.Quotas;
        filterCategory = filterId.HasValue ? Array.FindIndex(categories, x => x.Id == filterId) + 1 : 0;
        if (recipeId.HasValue) recipeChoice = Math.Max(0, Array.FindIndex(definitions.Where(x => !x.Retired && x.Enabled && productionTypes.Contains(x.TypeId)).ToArray(), x => x.Id == recipeId));
        if (state.Details?.Holding.Id == holdingSelection) details = state.Details;
        else if (holdingSelection.HasValue && !holdings.Any(x => x.Id == holdingSelection)) { details = null; holdingSelection = null; }

        LoadPins();
    }
    private void ClearInventoryViews()
    {
        textEditor.Clear();
        categories = []; definitions = []; holdings = []; compact = []; memberHoldings = []; quotas = null; details = null;
        holdingSelection = memberInventoryFor = memberInventoryRequestedFor = null; holdingCursor = compactCursor = memberHoldingCursor = null;
        pinned = []; filterType = ""; filterCategory = sort = loadedInventoryTab = inventoryTab = 0; search = ""; appliedInventoryQuery = "sort=name";
        memberHoldingSelection = null;
        discardTarget = null; removeItemsConfirmed = false; discardQuantity = removeQuantity = 1; itemRemovalReason = "";
        ClearCreationDrafts(); destination = -1;
    }
    private void DrawQuota(string kind)
    {
        var quota = quotas?.Items.FirstOrDefault(x => x.Kind == kind);
        if (quota == null) return;
        var label = kind switch
        {
            "potion_points" => "Alchemy",
            "cooking_points" => "Cooking",
            AmorRP.Core.Combat.CombatRules.BlacksmithingQuota => "Blacksmithing",
            _ => null
        };
        Wrapped(label == null ? $"You can create {quota.Remaining} more written items this week."
            : $"Weekly {label} points: {quota.Remaining} of {quota.Limit} remaining");
        Wrapped($"Next reset: {quota.ResetAt.LocalDateTime:g} (your local time)");
    }
    // One type registry feeds both the fixed category tabs and All's dependent
    // type choices. Add future types to their family here.
    private static readonly (string Id, int Tab)[] inventoryTypes = AmorRP.Core.Items.CraftingTypes.Consumables.Select(x => (Id: x, Tab: 1)).Concat(CraftingTypes.Writing.Select(x => (Id: x, Tab: 2))).Concat(CraftingTypes.Blacksmithing.Select(x => (Id: x, Tab: 3))).ToArray();
    private string[] InventoryTypes()
    {
        var allowed = inventoryTab == 0 && filterCategory > 0 && filterCategory <= categories.Length ? categories[filterCategory - 1].AllowedTypeIds : null;
        return inventoryTypes.Where(x => (inventoryTab == 0 || x.Tab == inventoryTab) && (allowed == null || allowed.Contains(x.Id))).Select(x => x.Id).ToArray();
    }
    private void ReadHolding(Holding row)
    {
        holdingSelection = row.Id; details = null;
        var path = $"api/v2/seasons/{community!.Id}/inventory/{row.Id}"; var token = session!.AccessToken;
        Start(async ct => { var loaded = await client.GetAsync<HoldingDetails>(origin!, path, token, ct); return () => { if (holdingSelection == row.Id) details = loaded; }; }, "Reading selected item…");
    }
    private void ApplyInventoryFilters()
    {
        appliedInventoryQuery = BuildInventoryQuery(); requests.ChangeData();
        holdings = []; holdingCursor = null; holdingSelection = null; details = null;
        RefreshAffected(RefreshTargets.Inventory);
    }
    private void DrawInventory()
    {
        if (community == null) { DrawNoSeason(); return; }
        DrawRefreshState(inventoryRefresh, "Inventory");
        if (!inventoryRefresh.HasLoaded && holdings.Length == 0) return;
        if (loadedInventoryTab != inventoryTab) { loadedInventoryTab = inventoryTab; filterType = ""; filterCategory = 0; ApplyInventoryFilters(); }
        ImGui.SetNextItemWidth(Math.Max(140, ImGui.GetContentRegionAvail().X - 85));
        var enter = ImGui.InputTextWithHint("##InventorySearch", "Search inventory…", ref search, 400, ImGuiInputTextFlags.EnterReturnsTrue);
        if (enter || ImGui.IsItemDeactivatedAfterEdit()) ApplyInventoryFilters();
        ImGui.SameLine(); if (ImGui.Button("Clear")) { search = filterType = ""; filterCategory = sort = 0; ApplyInventoryFilters(); }
        if (ImGui.BeginTable("InventoryFilters", inventoryTab == 0 ? 3 : 2, ImGuiTableFlags.SizingStretchSame))
        {
            if (inventoryTab == 0)
            {
                ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
                var cats = new[] { "All categories" }.Concat(categories.Select(x => x.Name)).ToArray(); filterCategory = Math.Clamp(filterCategory, 0, cats.Length - 1);
                if (ImGui.Combo("##InventoryCategory", ref filterCategory, cats, cats.Length)) { filterType = ""; ApplyInventoryFilters(); }
            }
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            var typeIds = new[] { "" }.Concat(InventoryTypes()).ToArray();
            var names = typeIds.Select(x => x.Length == 0 ? "All item types" : TypeName(x)).ToArray();
            var type = Math.Max(0, Array.IndexOf(typeIds, filterType));
            if (ImGui.Combo("##InventoryType", ref type, names, names.Length)) { filterType = typeIds[type]; ApplyInventoryFilters(); }
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##InventorySort", ref sort, new[] { "Sort: name", "Sort: type", "Sort: category" }, 3)) ApplyInventoryFilters();
            ImGui.EndTable();
        }
        var narrow = ImGui.GetContentRegionAvail().X < ImGui.CalcTextSize("Inventory details and available quantities").X * 1.7f;
        if (!ImGui.BeginTable("InventoryLayout", narrow ? 1 : 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Items", ImGuiTableColumnFlags.WidthStretch, .47f); if (!narrow) ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, .53f);
        ImGui.TableNextColumn(); ImGui.BeginChild("InventoryRows", new(0, narrow ? ImGui.GetTextLineHeightWithSpacing() * 9 : 0), true);
        var visible = holdings.Where(x => InventoryTypes().Contains(x.TypeId) && (filterType.Length == 0 || x.TypeId == filterType)).ToArray();
        DrawHoldingRows("OwnHoldings", visible, holdingSelection, ReadHolding);
        if (visible.Length == 0) Wrapped("No items match this view. Clear the filters, create an item in Crafting or receive one in a trade.");
        LoadMoreInventory(false); ImGui.EndChild(); ImGui.TableNextColumn();
        ImGui.BeginChild("InventoryDetails", new(0, 0), true); ImGui.PushItemWidth(-1);
        if (details == null) Wrapped(holdingSelection.HasValue ? "Reading the selected item…" : "Select an item on the left to view its details and actions.");
        var openDiscard = DrawDetails(details);
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
        DrawDiscardDialog(details?.Holding, openDiscard);
    }

    private void LoadMoreInventory(bool small)
    {
        var cursor = small ? compactCursor : holdingCursor;
        if (cursor == null || !ActionButton(small ? "More consumables" : "More inventory items", mutation: false)) return;
        var path = $"api/v2/seasons/{community!.Id}/inventory?" + (small ? "typeIds=potion,poison,explosive,drug,medicine,meal,drink,dessert&sort=name" : InventoryQuery()) + "&cursor=" + Uri.EscapeDataString(cursor);
        var token = session!.AccessToken; var query = InventoryQuery();
        Start(async ct => { var page = await client.GetAsync<HoldingPage>(origin!, path, token, ct); return () => {
            if (!small && query != InventoryQuery()) return;
            if (small) { compact = compact.Concat(page.Items).DistinctBy(x => x.Id).ToArray(); compactCursor = page.NextCursor; }
            else { holdings = holdings.Concat(page.Items).DistinctBy(x => x.Id).ToArray(); holdingCursor = page.NextCursor; }
        }; }, "Loading inventory page…");
    }
    private bool DrawDetails(HoldingDetails? selectedItem)
    {
        if (selectedItem == null) return false;
        var item = selectedItem.Holding;
        var definition = selectedItem.Definition;
        ImGui.PushID(item.Id.ToString());
        if (definition != null && CraftingTypes.IsRecipe(item.TypeId) && !CraftingTypes.IsGear(item.TypeId))
        {
            DrawConsumeButton(selectedItem);
            ImGui.SameLine();
        }
        else if (definition != null && CraftingTypes.IsSupply(item.TypeId))
        {
            DrawSupplyUseButton(selectedItem);
            ImGui.SameLine();
        }
        ImGui.BeginDisabled(item.Available < 1);
        var openDiscard = ActionButton("Discard", mutation: false);
        ImGui.EndDisabled();
        ImGui.PushStyleColor(ImGuiCol.Text, InventoryGold);
        Wrapped(item.Name);
        ImGui.PopStyleColor();
        if (definition != null && definition.Description.Length > 0) Wrapped(definition.Description);
        if (definition != null && CraftingTypes.IsRecipe(item.TypeId) && !CraftingTypes.IsGear(item.TypeId)) DrawConsumptionDetails(definition);
        else if (definition?.Gear is { } gear) ImGui.TextUnformatted(GearLabel(gear));
        else if (definition != null && CraftingTypes.IsSupply(item.TypeId))
            Wrapped($"Adds {definition.BonusPoints} {(item.TypeId == CraftingTypes.AlchemySupplies ? "Alchemy" : "Cooking")} points until the weekly reset.");
        if (selectedItem.Letter != null && ImGui.SmallButton("Read in Writing Log")) OpenWritingLog(item);
        if (definition != null && CraftingTypes.IsConsumable(item.TypeId) && ImGui.SmallButton(pinned.Contains(item.Id) ? "Unpin from consumables" : "Pin to consumables")) TogglePin(item.Id);
        ImGui.PopID();
        return openDiscard;
    }

    private void DrawDiscardDialog(Holding? item, bool open)
    {
        if (open)
        {
            discardTarget = item;
            discardQuantity = 1;
            ImGui.OpenPopup("Discard item");
        }
        if (!ImGui.BeginPopupModal("Discard item", ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextUnformatted(discardTarget?.Name ?? "Item unavailable");
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 10);
        ImGui.InputInt("Amount##DiscardQuantity", ref discardQuantity);
        var error = item == null || discardTarget?.Id != item.Id || discardTarget.Version != item.Version
            ? "This item changed. Reopen Discard to see the latest quantity."
            : discardQuantity < 1 || discardQuantity > item.Available ? $"Choose 1–{item.Available} available copies." : null;
        if (error != null) Wrapped(error);
        ImGui.BeginDisabled(error != null);
        if (ActionButton("Confirm") && item != null)
        {
            Mutate("POST", $"api/v2/seasons/{community!.Id}/inventory/{item.Id}/discard", new DiscardRequest(discardQuantity, item.Version));
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
    private string PinScope => origin!.AbsoluteUri + "|" + session!.Character.Id + "|" + community!.Id;
    private void LoadPins() { if (community != null) pinned = configuration.PinnedHoldings.TryGetValue(PinScope, out var pins) ? pins.ToHashSet() : []; }
    private void TogglePin(Guid id) { if (!pinned.Add(id)) pinned.Remove(id); configuration.PinnedHoldings[PinScope] = pinned.ToArray(); save(); }
    private void ApplyInventorySuccess(CommandRequest request, CommandResponse response, bool recovering)
    {
        NoteWritingLogCompletion(request, response);
        if (request.Operation.Kind == CommandKind.CreateRecipe)
        {
            var recipe = JsonSerializer.Deserialize<CommandResult<Definition>>(response.Json, FoundationClient.Json)!.Result;
            // Binding the receipt must not redirect a new/different draft that
            // the player opened while the request was in flight.
            if (definitionEditId == null && RecipeType == recipe.TypeId && definitionName.Trim() == recipe.Name && definitionDescription == recipe.Description
                && definitionMessage == recipe.Potion.UseMessage && definitionCost == recipe.Potion.CreationCost && definitionEnabled == recipe.Enabled)
            { definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision; }
        }
        if (request.Operation.Kind == CommandKind.EditRecipe)
        {
            var recipe = JsonSerializer.Deserialize<CommandResult<Definition>>(response.Json, FoundationClient.Json)!.Result;
            if (definitionEditId == recipe.Id) definitionEditRevision = Math.Max(definitionEditRevision ?? 0, recipe.Revision);
        }
        if (request.Operation.Kind == CommandKind.EditWriting)
        {
            var written = JsonSerializer.Deserialize<CommandResult<Letter>>(response.Json, FoundationClient.Json)!.Result;
            if (editingLetter == written.Id) letterEditVersion = Math.Max(letterEditVersion, written.Version);
        }
        if (request.Operation.Kind == CommandKind.CreateWriting && request.Json != null)
        {
            var submittedWriting = JsonSerializer.Deserialize<LetterCreate>(request.Json, FoundationClient.Json)!;
            if (letterTitle == submittedWriting.Title && letterBody == submittedWriting.Body) letterTitle = letterBody = "";
        }
        Holding? changed = null;
        ConsumptionResult? consumption = null;
        if (request.Operation.Kind is CommandKind.CraftConsumables or CommandKind.CreateWriting)
        {
            var produced = JsonSerializer.Deserialize<CommandResult<ProductionResult>>(response.Json, FoundationClient.Json)!.Result;
            changed = produced.Holding;
            if (quotas != null) quotas = quotas with { Items = quotas.Items.Select(x => x.Kind == produced.Quota.Kind ? produced.Quota : x).ToArray() };
        }
        else if (request.Operation.Kind is CommandKind.ChangeHolding or CommandKind.DeleteHolding or CommandKind.DiscardHolding or CommandKind.RemoveInventory)
            changed = JsonSerializer.Deserialize<CommandResult<Holding>>(response.Json, FoundationClient.Json)!.Result;
        else if (request.Operation.Kind == CommandKind.UseItem)
        {
            consumption = JsonSerializer.Deserialize<CommandResult<ConsumptionResult>>(response.Json, FoundationClient.Json)!.Result;
            changed = consumption.Holding;
        }
        if (changed != null)
        {
            var h = changed;
            Holding[] Apply(Holding[] list) => list.Where(x => x.Id != h.Id).Concat(h.Quantity > 0 && list.Any(x => x.Id == h.Id) ? new[] { h } : Array.Empty<Holding>()).OrderBy(x => x.Name).ToArray();
            holdings = Apply(holdings); compact = Apply(compact); memberHoldings = Apply(memberHoldings);
            if (details?.Holding.Id == h.Id) details = h.Quantity == 0 ? null : details with { Holding = h };
            // Preserve the server's selected sort order until the refreshed page arrives.
            companionHoldings = companionHoldings with { Items = companionHoldings.Items
                .Where(x => x.Id != h.Id || h.Quantity > 0).Select(x => x.Id == h.Id ? h : x).ToArray() };
            if (companionDetails?.Holding.Id == h.Id)
            {
                companionDetails = h.Quantity == 0 ? null : companionDetails with { Holding = h };
                if (h.Quantity == 0) companionSelection = null;
            }
            if (request.Operation.Kind is CommandKind.ChangeHolding or CommandKind.DeleteHolding or CommandKind.DiscardHolding or CommandKind.RemoveInventory)
            removeItemsConfirmed = false;
        }
        if (consumption == null) return;
        combatRefresh.Invalidate();
        actionResult = "Item consumed.";
        if (consumption.Destination.Length == 0) return;
        var channel = DestinationIndex(consumption.Destination);
        var submitted = !recovering && !response.Replayed && CanPost(consumption.Message, channel);
        if (submitted) lastPost = DateTimeOffset.UtcNow;
        actionResult = submitted && GameChatSender.Send(consumption.Message, channel, ChatLimit)
            ? "Item consumed. Message submitted to the chosen game channel; delivery is not confirmed."
            : "Item consumed. The chat message was not submitted.";
    }
}
