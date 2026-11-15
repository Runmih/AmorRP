using System.Numerics;
using System.Net;
using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Features.Inventory;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private static readonly Vector4 InventoryGold = new(.88f, .75f, .47f, 1);
    private readonly InventoryCompanionQuery companionQuery = new();
    private readonly BackgroundRefresh<CompanionInventoryRead> companionInventoryRefresh = new(TimeSpan.FromMinutes(1));
    private HoldingPage companionHoldings = new([], null, "0");
    private HoldingDetails? companionDetails;
    private Guid? companionSelection;
    private string companionSearch = "";
    private bool companionVisible;
    private bool companionDiscardConfirmed;
    private int companionDiscardQuantity = 1;
    private long companionViewVersion;
    private sealed record CompanionInventoryRead(HoldingPage Page, HoldingDetails? Details);

    public void SetInventoryCompanionVisible(bool visible)
    {
        if (visible && !companionVisible)
        {
            companionInventoryRefresh.Invalidate();
            myBillsRefresh.Invalidate();
            refreshNeeded = true;
        }
        companionVisible = visible;
    }

    private void ResetInventoryCompanion()
    {
        companionInventoryRefresh.Reset(); companionQuery.Reset();
        companionHoldings = new([], null, "0"); companionDetails = null;
        companionSelection = null; companionSearch = ""; companionViewVersion++;
        companionDiscardConfirmed = false; companionDiscardQuantity = 1;
    }

    private void CollectInventoryCompanion(DateTimeOffset now)
    {
        if (!companionInventoryRefresh.TryCollect(now, dataGeneration, out var state)) return;
        companionHoldings = state.Page; companionDetails = state.Details;
        if (companionSelection.HasValue && state.Details == null) companionSelection = null;
    }

    private void StartInventoryCompanion(DateTimeOffset now, Uri uri, string token, Guid communityId, CancellationToken ct)
    {
        if (!companionVisible || !companionQuery.CanLoad || Busy) return;
        var query = companionQuery.Build(); var selectedItem = companionSelection; var count = companionHoldings.Items.Length;
        companionInventoryRefresh.Start(now, dataGeneration, ct,
            c => LoadInventoryCompanionAsync(uri, token, communityId, query, selectedItem, count, c));
    }

    private async Task<CompanionInventoryRead> LoadInventoryCompanionAsync(Uri uri, string token, Guid id,
        string query, Guid? selectedItem, int requestedCount, CancellationToken ct)
    {
        var prefix = $"api/v2/seasons/{id}/inventory";
        var page = await client.GetAsync<HoldingPage>(uri, prefix + "?" + query, token, ct);
        var rows = page.Items.ToList();
        while (rows.Count < requestedCount && page.NextCursor != null)
        {
            page = await client.GetAsync<HoldingPage>(uri, prefix + "?" + query + "&cursor=" + Uri.EscapeDataString(page.NextCursor), token, ct);
            rows.AddRange(page.Items);
        }
        HoldingDetails? selectedDetails = null;
        if (selectedItem.HasValue)
        {
            try { selectedDetails = await client.GetAsync<HoldingDetails>(uri, prefix + "/" + selectedItem, token, ct); }
            catch (FoundationApiException error) when (error.Status == HttpStatusCode.NotFound) { }
        }
        return new(page with { Items = rows.ToArray() }, selectedDetails);
    }

    private void ApplyCompanionFilters()
    {
        companionQuery.Search = companionSearch;
        companionSelection = null; companionDetails = null; companionDiscardConfirmed = false; companionViewVersion++;
        companionDiscardQuantity = 1;
        companionHoldings = new([], null, "0"); companionInventoryRefresh.Reset();
    }

    private void DrawInventoryCompanion(PendingCommand? pending)
    {
        ImGui.TextColored(InventoryGold, "AMOR RP  /  INVENTORY");
        DrawCommunitySelector("##InventoryCommunity", -1);
        DrawSeasonState();
        if (community == null)
        {
            ImGui.Spacing(); DrawNoSeason();
            DrawActionFeedback(pending);
            return;
        }

        var footerHeight = ImGui.GetTextLineHeightWithSpacing() * 4 + ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 4;
        if (community.MyBalance.HeldForDebt != "0") footerHeight += ImGui.GetTextLineHeightWithSpacing();
        var feedbackHeight = ActionFeedbackHeight(pending);
        var bodyHeight = Math.Max(ImGui.GetFrameHeight(), ImGui.GetContentRegionAvail().Y - footerHeight - feedbackHeight - ImGui.GetStyle().ItemSpacing.Y * 2);
        ImGui.BeginChild("CompanionItems", new(0, bodyHeight), false);
        DrawCompanionBrowser();
        ImGui.EndChild();
        DrawActionFeedback(pending);
        ImGui.BeginChild("CompanionFinances", new(0, footerHeight), false);
        DrawCompanionFinances();
        ImGui.EndChild();
        DrawSharedDialogsOnce();
    }

    private void DrawCompanionBrowser()
    {
        if (ImGui.BeginTabBar("CompanionItemFamilies", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            foreach (var section in Enum.GetValues<InventoryCompanionSection>())
            {
                if (!ImGui.BeginTabItem(section.ToString())) continue;
                if (companionQuery.Section != section)
                { companionQuery.Section = section; companionQuery.Type = ""; ApplyCompanionFilters(); }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
        ImGui.SetNextItemWidth(Math.Max(80, ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight() * 2.6f));
        var entered = ImGui.InputTextWithHint("##CompanionSearch", "Search items…", ref companionSearch, 100, ImGuiInputTextFlags.EnterReturnsTrue);
        if (entered || ImGui.IsItemDeactivatedAfterEdit()) ApplyCompanionFilters();
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
        { companionSearch = companionQuery.Type = ""; companionQuery.Sort = 0; ApplyCompanionFilters(); }

        if (!companionQuery.CanLoad)
        {
            ImGui.Spacing(); ImGui.TextColored(InventoryGold, companionQuery.Section.ToString());
            Wrapped(companionQuery.Section == InventoryCompanionSection.Supplies
                ? "Delivery packages and crafting supplies will appear here when available."
                : "Registered permits and other RP documents will appear here when available.");
            return;
        }
        if (ImGui.BeginTable("CompanionFilters", 2, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            var types = new[] { "" }.Concat(companionQuery.Types).ToArray();
            var names = types.Select(x => x.Length == 0 ? "All item types" : TypeName(x)).ToArray();
            var type = Math.Max(0, Array.IndexOf(types, companionQuery.Type));
            if (ImGui.Combo("##CompanionType", ref type, names, names.Length))
            { companionQuery.Type = types[type]; ApplyCompanionFilters(); }
            ImGui.TableNextColumn(); ImGui.SetNextItemWidth(-1);
            var sorting = companionQuery.Sort;
            if (ImGui.Combo("##CompanionSort", ref sorting, new[] { "Sort: name", "Sort: type", "Sort: category" }, 3))
            { companionQuery.Sort = sorting; ApplyCompanionFilters(); }
            ImGui.EndTable();
        }
        DrawRefreshState(companionInventoryRefresh, "Inventory");
        if (!companionInventoryRefresh.HasLoaded && companionHoldings.Items.Length == 0) return;
        var available = ImGui.GetContentRegionAvail().Y;
        var listHeight = companionSelection.HasValue ? Math.Max(ImGui.GetFrameHeight() * 2, available * .48f) : 0;
        ImGui.BeginChild("CompanionItemList", new(0, listHeight), true);
        if (ImGui.BeginTable("CompanionHoldings", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Available", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Available").X);
            ImGui.TableSetupColumn("Reserved", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Reserved").X);
            ImGui.TableHeadersRow();
            foreach (var item in companionHoldings.Items)
            {
                ImGui.PushID(item.Id.ToString()); ImGui.TableNextRow(); ImGui.TableNextColumn();
                DrawItemTypeIcon(item.TypeId); ImGui.SameLine();
                if (ImGui.Selectable(item.Name + "###CompanionHolding", companionSelection == item.Id, ImGuiSelectableFlags.SpanAllColumns))
                {
                    companionSelection = item.Id; companionDetails = null; companionDiscardConfirmed = false; companionDiscardQuantity = 1;
                    companionInventoryRefresh.Invalidate();
                }
                ImGui.TextDisabled(TypeName(item.TypeId));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(item.Available.ToString("N0"));
                ImGui.TableNextColumn(); ImGui.TextDisabled(item.Reserved.ToString("N0"));
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (companionHoldings.Items.Length == 0)
        { ImGui.Spacing(); Wrapped("No matching items. Try another tab or clear your filters."); }
        if (companionHoldings.NextCursor != null && ActionButton("Load more items", mutation: false)) LoadMoreCompanionItems();
        ImGui.EndChild();
        if (!companionSelection.HasValue) return;
        ImGui.BeginChild("CompanionItemDetails", new(0, 0), true);
        ImGui.PushItemWidth(-1);
        if (ImGui.SmallButton("Back to full list")) { companionSelection = null; companionDetails = null; }
        else if (companionDetails != null) DrawDetails(companionDetails, ref companionDiscardConfirmed, ref companionDiscardQuantity);
        else Wrapped("Loading item details…");
        ImGui.PopItemWidth(); ImGui.EndChild();
    }

    private void LoadMoreCompanionItems()
    {
        var query = companionQuery.Build(); var id = community!.Id; var cursor = companionHoldings.NextCursor;
        var uri = origin!; var token = session!.AccessToken;
        var viewVersion = companionViewVersion;
        companionInventoryRefresh.Invalidate();
        Start(async ct =>
        {
            var page = await client.GetAsync<HoldingPage>(uri, $"api/v2/seasons/{id}/inventory?{query}&cursor=" + Uri.EscapeDataString(cursor!), token, ct);
            return () =>
            {
                if (community?.Id != id || companionViewVersion != viewVersion || !companionQuery.CanLoad || companionQuery.Build() != query || companionHoldings.NextCursor != cursor) return;
                companionHoldings = page with { Items = companionHoldings.Items.Concat(page.Items).DistinctBy(x => x.Id).ToArray() };
            };
        }, "Loading more items…");
    }

    private void DrawCompanionFinances()
    {
        if (community == null) return;
        ImGui.Separator(); ImGui.Spacing();
        ImGui.TextDisabled("PERSONAL FINANCES");
        ImGui.TextColored(InventoryGold, InventoryCompanionQuery.FormatAmount(community.MyBalance.Available) + " " + community.Currency.Name + " available");
        if (ImGui.BeginTable("CompanionBalances", 3, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn(); ImGui.TextDisabled("Total coins");
            ImGui.TextUnformatted(InventoryCompanionQuery.FormatAmount(community.MyBalance.Owned));
            ImGui.TableNextColumn(); ImGui.TextDisabled("Reserved");
            ImGui.TextUnformatted(InventoryCompanionQuery.FormatAmount(community.MyBalance.Reserved));
            ImGui.TableNextColumn(); ImGui.TextDisabled("Debt owed");
            ImGui.TextUnformatted(InventoryCompanionQuery.FormatAmount(myBillsRefresh.HasLoaded ? myBills.TotalOutstanding : null));
            ImGui.EndTable();
        }
        if (community.MyBalance.HeldForDebt != "0")
            Wrapped(InventoryCompanionQuery.FormatAmount(community.MyBalance.HeldForDebt) + (SeasonReadOnly ? " coins held for recorded debts in this season" : " coins awaiting automatic repayment"));
        if (ImGui.SmallButton("View bills")) { requestedCommunityPage = CommunityBrowserPage.Overview; OpenWorkspace(AmorRP.Plugin.Features.Shell.WorkspaceKind.Season); }
        ImGui.SameLine();
        if (ImGui.SmallButton("Refresh")) { companionInventoryRefresh.Invalidate(); myBillsRefresh.Invalidate(); refreshNeeded = true; }
        if (myBillsRefresh.Error != null) { ImGui.SameLine(); ImGui.TextDisabled("Debt update unavailable"); }
    }
}
