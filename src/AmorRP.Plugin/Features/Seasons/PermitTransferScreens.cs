using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Permits;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private Guid? transferPermit, transferBuyerGroup;
    private string transferPrice = "";

    private void DrawPermitTransferOffers()
    {
        if (permitShop?.Transfers is not { Length: > 0 } offers) return;
        var actor = session!.Character.Id;
        foreach (var offer in offers)
        {
            ImGui.PushID(offer.Id.ToString());
            Wrapped($"{offer.CharacterName} · {offer.SellerGroup} to {offer.BuyerGroup} · {offer.Price} {community!.Currency.Name}");
            var path = $"api/v2/seasons/{community.Id}/permit-transfers/{offer.Id}";
            if (actor == offer.CharacterId && !offer.Approved)
            {
                if (ActionButton("Allow transfer")) Mutate("PUT", path + "/approval", null);
                ImGui.SameLine();
            }
            else if (actor == offer.BuyerId && offer.Approved)
            {
                if (ActionButton("Accept transfer")) Mutate("PUT", path + "/acceptance", null);
                ImGui.SameLine();
            }
            else
            {
                ImGui.TextDisabled(offer.Approved ? "Awaiting buyer" : "Awaiting character approval");
                ImGui.SameLine();
            }
            if (ActionButton(actor == offer.SellerId ? "Cancel offer" : "Decline offer")) Mutate("DELETE", path, null);
            ImGui.PopID();
        }
    }

    private void DrawPermitTransferDraft(OwnershipPermit permit)
    {
        if (!permitShop!.Policy.Enabled || permitShop.Transfers?.Any(x => x.PermitId == permit.Id) == true) return;
        ImGui.SameLine();
        if (ImGui.SmallButton("Offer transfer"))
        {
            transferPermit = transferPermit == permit.Id ? null : permit.Id;
            transferBuyerGroup = null;
            transferPrice = "0";
        }
        if (transferPermit != permit.Id) return;
        var targets = communityGroups.Where(x => x.OwnerCharacterId != session!.Character.Id && x.OwnerCharacterId != permit.CharacterId
            && x.EnabledFeatures?.Contains(GroupCapabilities.Treasury) == true).ToArray();
        var target = targets.FirstOrDefault(x => x.Id == transferBuyerGroup);
        if (ImGui.BeginCombo("Receiving group", target?.Name ?? "Choose a group"))
        {
            foreach (var group in targets)
                if (ImGui.Selectable(group.Name + "###permitTransfer" + group.Id, group.Id == transferBuyerGroup)) transferBuyerGroup = group.Id;
            ImGui.EndCombo();
        }
        ImGui.InputText("Price", ref transferPrice, 21, ImGuiInputTextFlags.CharsDecimal);
        var valid = WholeUnits.TryNormalize(transferPrice, out var price, out var units) && units >= 0;
        ImGui.BeginDisabled(target == null || !valid);
        if (ActionButton("Send transfer offer") && target != null)
            Mutate("POST", PermitsPath + $"/{permit.Id}/transfers", new PermitTransferCreate(target.Id, price));
        ImGui.EndDisabled();
    }
}
