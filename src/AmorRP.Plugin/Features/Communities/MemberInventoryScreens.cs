using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Inventory;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private Guid? memberInventoryRequestedFor;
    private void LoadMemberInventory(Member target)
    {
        memberInventoryRequestedFor = target.Character.Id;
        var path = $"api/v2/communities/{community!.Id}/members/{target.Character.Id}/holdings"; var token = session!.AccessToken;
        Start(async ct => {
            var page = await client.GetAsync<HoldingPage>(origin!, path, token, ct);
            return () => {
                if (memberSelection != target.Character.Id) return;
                memberHoldings = page.Items; memberInventoryFor = target.Character.Id; memberHoldingCursor = page.NextCursor;
            };
        }, "Reading member item summaries…");
    }
    private void DrawMemberInventory(Member target)
    {
        if (!(Owner || community!.MyCapabilities.Contains("inventory.remove"))) return;
        if (memberInventoryRequestedFor != target.Character.Id) LoadMemberInventory(target);
        Wrapped("Item summaries only. Written contents stay private to the holder.");
        if (ActionButton("Refresh item summaries", mutation: false)) LoadMemberInventory(target);
        if (memberInventoryFor != target.Character.Id) { Wrapped("Waiting for this member's item summaries. Any error appears in the action/status area above."); return; }
        ImGui.BeginChild("MemberHoldings", new(0, ImGui.GetTextLineHeightWithSpacing() * 9), true);
        DrawHoldingRows("ManagedHoldings", memberHoldings, memberHoldingSelection, item => { memberHoldingSelection = item.Id; removeQuantity = 1; removeItemsConfirmed = false; });
        if (memberHoldings.Length == 0) Wrapped("This member has no items.");
        ImGui.EndChild();
        if (memberHoldingCursor != null && ActionButton("More items", mutation: false))
        {
            var path = $"api/v2/communities/{community!.Id}/members/{target.Character.Id}/holdings?cursor=" + Uri.EscapeDataString(memberHoldingCursor); var token = session!.AccessToken;
            Start(async ct => { var page = await client.GetAsync<HoldingPage>(origin!, path, token, ct); return () => {
                if (memberSelection != target.Character.Id || memberInventoryFor != target.Character.Id) return;
                memberHoldings = memberHoldings.Concat(page.Items).DistinctBy(x => x.Id).ToArray(); memberHoldingCursor = page.NextCursor;
            }; }, "Reading more member items…");
        }
        var item = memberHoldings.FirstOrDefault(x => x.Id == memberHoldingSelection);
        if (item == null) { Wrapped(memberHoldingSelection.HasValue ? "The selected item is no longer in this inventory. Select another row." : "Select the item you want to remove from the list above."); return; }
        ImGui.Separator(); Wrapped($"Selected: {item.Name} · held by {target.Character.DisplayName}");
        Wrapped($"{item.Available} available · {item.Reserved} reserved");
        if (target.Status != "active") { Wrapped("This member is inactive. Their retained items cannot be changed."); return; }
        FieldLabel("Quantity to remove"); if (ImGui.InputInt("##MemberRemoveQuantity", ref removeQuantity)) removeItemsConfirmed = false;
        var quantityError = item.Available == 0 ? "All copies are reserved for trading. End that trade before removing them."
            : removeQuantity < 1 || removeQuantity > item.Available ? $"Choose between 1 and {item.Available} copies." : null;
        FieldNote("MemberRemoveQuantity", quantityError, $"Remove {removeQuantity} × {item.Name} from {target.Character.DisplayName}");
        FieldLabel("Reason for removal"); LimitedInput("##MemberRemoveReason", ref itemRemovalReason, 500, countCodeUnits: true);
        var reasonValid = !string.IsNullOrWhiteSpace(itemRemovalReason) && itemRemovalReason.Length <= 500 && !itemRemovalReason.Any(c => char.IsControl(c) && c != '\n');
        FieldNote("MemberRemoveReason", reasonValid ? null : "A reason of at most 500 characters is required.", "The reason and both characters appear in community history. No allowance is refunded.");
        ImGui.Checkbox("I confirm removal of this selected quantity", ref removeItemsConfirmed);
        ImGui.BeginDisabled(!removeItemsConfirmed || !reasonValid || quantityError != null);
        if (ActionButton("Remove selected quantity")) Mutate("PATCH", $"api/v2/communities/{community!.Id}/members/{target.Character.Id}/holdings/{item.Id}", new HoldingChange(item.Version, item.Quantity - removeQuantity, itemRemovalReason));
        ImGui.EndDisabled();
    }
}
