using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
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
    private bool discardConfirmed;
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
    private async Task<InventoryState> LoadInventoryAsync(Uri uri, string token, Guid id, CancellationToken ct)
    {
        var query = InventoryQuery(); var requestedSelection = holdingSelection; var requestedCount = holdings.Length;
        var prefix = $"api/v1/groups/{id}/";
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
        var potions = await client.GetAsync<HoldingPage>(uri, prefix + "inventory?typeId=potion&sort=name", token, ct);
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
        var currentRecipes = definitions.Where(x => !x.Retired && x.Enabled).ToArray();
        var recipeId = recipeChoice < currentRecipes.Length ? (Guid?)currentRecipes[recipeChoice].Id : null;
        categories = state.Categories; definitions = state.Definitions; holdings = state.Holdings.Items; holdingCursor = state.Holdings.NextCursor;
        compact = state.Compact.Items; compactCursor = state.Compact.NextCursor; quotas = state.Quotas;
        filterCategory = filterId.HasValue ? Array.FindIndex(categories, x => x.Id == filterId) + 1 : 0;
        if (recipeId.HasValue) recipeChoice = Math.Max(0, Array.FindIndex(definitions.Where(x => !x.Retired && x.Enabled).ToArray(), x => x.Id == recipeId));
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
        discardConfirmed = removeItemsConfirmed = false; discardQuantity = removeQuantity = 1; itemRemovalReason = "";
        ClearCreationDrafts(); lastUse = null;
    }
    private void DrawQuota(string kind)
    {
        var quota = quotas?.Items.FirstOrDefault(x => x.Kind == kind);
        if (quota == null) return;
        Wrapped($"{(kind == "potion_points" ? "Potion points" : "Writing allowance")}: {quota.Remaining}/{quota.Limit} remaining");
        Wrapped($"Resets: {quota.ResetAt.LocalDateTime:g}");
    }
    // One type registry feeds both the fixed category tabs and All's dependent
    // type choices. Add future types to their family here.
    private static readonly (string Id, int Tab)[] inventoryTypes = [("potion", 1), ("book", 2), ("letter", 2), ("note", 2)];
    private string[] InventoryTypes()
    {
        var allowed = inventoryTab == 0 && filterCategory > 0 && filterCategory <= categories.Length ? categories[filterCategory - 1].AllowedTypeIds : null;
        return inventoryTypes.Where(x => (inventoryTab == 0 || x.Tab == inventoryTab) && (allowed == null || allowed.Contains(x.Id))).Select(x => x.Id).ToArray();
    }
    private void ReadHolding(Holding row)
    {
        holdingSelection = row.Id; details = null; discardConfirmed = false;
        var path = $"api/v1/groups/{group!.Id}/inventory/{row.Id}"; var token = session!.AccessToken;
        Start(async ct => { var loaded = await client.GetAsync<HoldingDetails>(origin!, path, token, ct); return () => { if (holdingSelection == row.Id) details = loaded; }; }, "Reading selected item…");
    }
    private void ApplyInventoryFilters()
    {
        appliedInventoryQuery = BuildInventoryQuery(); dataGeneration++;
        holdings = []; holdingCursor = null; holdingSelection = null; details = null;
        RefreshAffected(new("GET", $"api/v1/groups/{group!.Id}/inventory", null, null));
    }
    private void DrawInventory()
    {
        if (group == null) { Wrapped("Join or create a group first."); return; }
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
        if (!ImGui.BeginTable("InventoryLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV)) return;
        ImGui.TableSetupColumn("Items", ImGuiTableColumnFlags.WidthStretch, .47f); ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, .53f);
        ImGui.TableNextColumn(); ImGui.BeginChild("InventoryRows", new(0, 0), true);
        var visible = holdings.Where(x => InventoryTypes().Contains(x.TypeId) && (filterType.Length == 0 || x.TypeId == filterType)).ToArray();
        DrawHoldingRows("OwnHoldings", visible, holdingSelection, ReadHolding);
        if (visible.Length == 0) Wrapped("No items match this view. Clear the filters, create an item in Crafting or receive one in a trade.");
        LoadMoreInventory(false); ImGui.EndChild(); ImGui.TableNextColumn();
        ImGui.BeginChild("InventoryDetails", new(0, 0), true); ImGui.PushItemWidth(-1);
        if (details == null) Wrapped(holdingSelection.HasValue ? "Reading the selected item…" : "Select an item on the left to view its details and actions."); else DrawDetails();
        ImGui.PopItemWidth(); ImGui.EndChild(); ImGui.EndTable();
    }

    private void LoadMoreInventory(bool small)
    {
        var cursor = small ? compactCursor : holdingCursor;
        if (cursor == null || !ActionButton(small ? "More consumables" : "More inventory items", mutation: false)) return;
        var path = $"api/v1/groups/{group!.Id}/inventory?" + (small ? "typeId=potion&sort=name" : InventoryQuery()) + "&cursor=" + Uri.EscapeDataString(cursor);
        var token = session!.AccessToken; var query = InventoryQuery();
        Start(async ct => { var page = await client.GetAsync<HoldingPage>(origin!, path, token, ct); return () => {
            if (!small && query != InventoryQuery()) return;
            if (small) { compact = compact.Concat(page.Items).DistinctBy(x => x.Id).ToArray(); compactCursor = page.NextCursor; }
            else { holdings = holdings.Concat(page.Items).DistinctBy(x => x.Id).ToArray(); holdingCursor = page.NextCursor; }
        }; }, "Loading inventory page…");
    }
    private void DrawDetails()
    {
        if (details == null) return;
        var item = details.Holding; ImGui.TextUnformatted(item.Name);
        ImGui.TextDisabled(TypeName(item.TypeId) + " · " + (categories.FirstOrDefault(x => x.Id == item.CategoryId)?.Name ?? "Retained category"));
        Wrapped($"{item.Available} available · {item.Reserved} reserved for trading");
        ImGui.Separator();
        if (details.Definition != null)
        {
            Wrapped(details.Definition.Description); ImGui.TextDisabled("Recipe revision " + details.Definition.Revision);
            if (ImGui.Button(pinned.Contains(item.Id) ? "Unpin from consumables" : "Pin to consumables")) TogglePin(item.Id);
            DrawPotionUse(details);
        }
        if (details.Letter != null) DrawLetterDetails(details.Letter);
        if (lastUse != null && ImGui.CollapsingHeader("Last use / resend")) DrawResend();
        if (!ImGui.CollapsingHeader("Discard this item")) return;
        Wrapped("Discarding removes your own copies permanently and does not refund creation allowance.");
        FieldLabel("Quantity to discard"); if (ImGui.InputInt("##DiscardQuantity", ref discardQuantity)) discardConfirmed = false;
        var error = item.Available == 0 ? "All copies are reserved for a trade. End the trade before discarding them." : discardQuantity < 1 || discardQuantity > item.Available ? $"Choose 1–{item.Available} unreserved copies." : null;
        FieldNote("DiscardQuantity", error, $"Discard {discardQuantity} × {item.Name}");
        ImGui.Checkbox("I confirm this discard", ref discardConfirmed);
        ImGui.BeginDisabled(!discardConfirmed || error != null);
        if (ActionButton("Discard selected quantity")) Mutate("PATCH", $"api/v1/groups/{group!.Id}/members/{session!.Character.Id}/holdings/{item.Id}", new HoldingChange(item.Version, item.Quantity - discardQuantity));
        ImGui.EndDisabled();
    }
    private string PinScope => origin!.AbsoluteUri + "|" + session!.Character.Id + "|" + group!.Id;
    private void LoadPins() { if (group != null) pinned = configuration.PinnedHoldings.TryGetValue(PinScope, out var pins) ? pins.ToHashSet() : []; }
    private void TogglePin(Guid id) { if (!pinned.Add(id)) pinned.Remove(id); configuration.PinnedHoldings[PinScope] = pinned.ToArray(); save(); }
    private void ApplyInventorySuccess(CommandRequest request, CommandResponse response, bool recovering)
    {
        if (request.Method == "POST" && request.Path.EndsWith("/item-definitions", StringComparison.Ordinal))
        {
            var recipe = JsonSerializer.Deserialize<CommandResult<Definition>>(response.Json, FoundationClient.Json)!.Result;
            // Binding the receipt must not redirect a new/different draft that
            // the player opened while the request was in flight.
            if (definitionEditId == null && definitionName.Trim() == recipe.Name && definitionDescription == recipe.Description
                && definitionMessage == recipe.Potion.UseMessage && definitionCost == recipe.Potion.CreationCost && definitionEnabled == recipe.Enabled)
            { definitionEditId = recipe.Id; definitionEditRevision = recipe.Revision; }
        }
        if (request.Method == "PATCH" && request.Path.Contains("/item-definitions/", StringComparison.Ordinal))
        {
            var recipe = JsonSerializer.Deserialize<CommandResult<Definition>>(response.Json, FoundationClient.Json)!.Result;
            if (definitionEditId == recipe.Id) definitionEditRevision = Math.Max(definitionEditRevision ?? 0, recipe.Revision);
        }
        if (request.Method == "PATCH" && request.Path.Contains("/letters/", StringComparison.Ordinal))
        {
            var written = JsonSerializer.Deserialize<CommandResult<Letter>>(response.Json, FoundationClient.Json)!.Result;
            if (editingLetter == written.Id) letterEditVersion = Math.Max(letterEditVersion, written.Version);
        }
        if (request.Path.EndsWith("/inventory/letters", StringComparison.Ordinal) && request.Json != null)
        {
            var submittedWriting = JsonSerializer.Deserialize<LetterCreate>(request.Json, FoundationClient.Json)!;
            if (letterTitle == submittedWriting.Title && letterBody == submittedWriting.Body) letterTitle = letterBody = "";
        }
        Holding? changed = null;
        if (request.Path.EndsWith("/inventory/potions", StringComparison.Ordinal) || request.Path.EndsWith("/inventory/letters", StringComparison.Ordinal))
        {
            var produced = JsonSerializer.Deserialize<CommandResult<ProductionResult>>(response.Json, FoundationClient.Json)!.Result;
            changed = produced.Holding;
            if (quotas != null) quotas = quotas with { Items = quotas.Items.Select(x => x.Kind == produced.Quota.Kind ? produced.Quota : x).ToArray() };
        }
        else if (request.Path.Contains("/holdings/", StringComparison.Ordinal) || request.Path.EndsWith("/discard", StringComparison.Ordinal))
            changed = JsonSerializer.Deserialize<CommandResult<Holding>>(response.Json, FoundationClient.Json)!.Result;
        else if (request.Path.EndsWith("/use", StringComparison.Ordinal)) changed = JsonSerializer.Deserialize<CommandResult<ConsumptionResult>>(response.Json, FoundationClient.Json)!.Result.Holding;
        if (changed != null)
        {
            var h = changed;
            Holding[] Apply(Holding[] list) => list.Where(x => x.Id != h.Id).Concat(h.Quantity > 0 && list.Any(x => x.Id == h.Id) ? new[] { h } : Array.Empty<Holding>()).OrderBy(x => x.Name).ToArray();
            holdings = Apply(holdings); compact = Apply(compact); memberHoldings = Apply(memberHoldings);
            if (details?.Holding.Id == h.Id) details = h.Quantity == 0 ? null : details with { Holding = h };
            if (request.Path.Contains("/holdings/", StringComparison.Ordinal) || request.Path.EndsWith("/discard", StringComparison.Ordinal))
            { discardConfirmed = false; removeItemsConfirmed = false; }
        }
        if (!request.Path.EndsWith("/use", StringComparison.Ordinal)) return;
        var result = JsonSerializer.Deserialize<CommandResult<ConsumptionResult>>(response.Json, FoundationClient.Json)!.Result;
        lastUse = result; resendConfirmed = false;
        var destination = DestinationIndex(result.Destination);
        var submitted = !recovering && !response.Replayed && CanPost(result.Message, destination);
        if (submitted) lastPost = DateTimeOffset.UtcNow;
        actionResult = submitted && GameChatSender.Send(result.Message, destination, ChatLimit)
            ? "Potion consumed. Message submitted to the chosen game channel; delivery is not confirmed."
            : "Potion consumed. Chat was not submitted by this response. Review the outcome before a manual resend.";
    }
}
