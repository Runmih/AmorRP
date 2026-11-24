using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Permits;
using AmorRP.Contracts.Seasons;
using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private const string ManagePermitListings = "permits.list.manage";
    private readonly BackgroundRefresh<PermitShop> permitsRefresh = new(TimeSpan.FromSeconds(12));
    private PermitShop? permitShop;
    private string permitBasePrice = "", permitFeedback = "";
    private bool permitEnabled, permitTabRequested;
    private Guid? permitBuyerGroup;
    private CharacterTarget? privatePermitTarget;
    private Member? privatePermitMember;
    private bool CanNominatePermit => Owner || community?.MyCapabilities.Contains(ManagePermitListings) == true;
    public bool CanOfferPrivatePermit => session != null && community != null && !SeasonReadOnly
        && communityGroups.Any(CanBuyPermitForGroup);

    private string PermitsPath => $"api/v2/seasons/{community!.Id}/permits";

    private void ClearPermits()
    {
        permitsRefresh.Reset();
        permitShop = null;
        permitBasePrice = permitFeedback = "";
        permitEnabled = permitTabRequested = false;
        permitBuyerGroup = transferPermit = transferBuyerGroup = null;
        privatePermitTarget = null;
        privatePermitMember = null;
    }

    private void NominatePermit(Member member)
    {
        permitFeedback = "";
        permitTabRequested = true;
        Mutate("POST", PermitsPath + "/listings", new PermitListingEdit(member.Character.Id));
        OpenWorkspace(WorkspaceKind.Shop);
    }

    public void OpenPrivatePermit(CharacterTarget target)
    {
        if (!CanOfferPrivatePermit || session == null || community == null || origin == null) return;
        var uri = origin;
        var seasonId = community.Id;
        var actor = session.Character.Id;
        var token = session.AccessToken;
        privatePermitTarget = target;
        privatePermitMember = null;
        permitFeedback = "Finding " + target.Name + "…";
        permitTabRequested = true;
        OpenWorkspace(WorkspaceKind.Shop);
        Start(async ct =>
        {
            var match = await FindSeasonCharacterAsync(uri, token, seasonId, target, ct);
            return () =>
            {
                if (!ReferenceEquals(privatePermitTarget, target) || community?.Id != seasonId) return;
                privatePermitMember = match?.Character.Id == actor ? null : match;
                permitFeedback = privatePermitMember == null ? "Choose another verified character in this season." : "";
            };
        }, "Finding permit participant…");
    }

    private void DrawPermits()
    {
        var now = DateTimeOffset.UtcNow;
        if (permitsRefresh.TryCollect(now, dataGeneration, out var result))
        {
            if (permitShop == null || permitBasePrice == permitShop.Policy.BasePrice && permitEnabled == permitShop.Policy.Enabled)
            {
                permitBasePrice = result.Policy.BasePrice;
                permitEnabled = result.Policy.Enabled;
            }
            permitShop = result;
        }
        if (origin is { } uri && session is { } actor && !SessionRenewalInProgress)
        {
            var path = PermitsPath;
            permitsRefresh.Start(now, dataGeneration, requests.Token,
                ct => client.GetAsync<PermitShop>(uri, path, actor.AccessToken, ct));
        }
        DrawRefreshState(permitsRefresh, "Permits");
        if (permitFeedback.Length > 0) Wrapped(permitFeedback);
        if (permitShop == null) return;
        if (Owner && ImGui.CollapsingHeader("Permit settings"))
        {
            ImGui.Checkbox("Enable new permits", ref permitEnabled);
            ImGui.InputText("Base price", ref permitBasePrice, 21, ImGuiInputTextFlags.CharsDecimal);
            var valid = WholeUnits.TryNormalize(permitBasePrice, out var normalized, out var price) && price > 0;
            ImGui.BeginDisabled(!valid || normalized == permitShop.Policy.BasePrice && permitEnabled == permitShop.Policy.Enabled);
            if (ActionButton("Save permit settings")) Mutate("PUT", PermitsPath + "/policy", new PermitPolicy(normalized, permitEnabled));
            ImGui.EndDisabled();
        }
        var groups = communityGroups.Where(CanBuyPermitForGroup).ToArray();
        DrawPrivatePermitDraft(groups);
        DrawPermitOffers();
        DrawPermitTransferOffers();
        DrawPermitListings(groups);
        if (permitShop.Permits.Length > 0 && ImGui.CollapsingHeader("Registered permits", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var permit in permitShop.Permits)
            {
                ImGui.PushID(permit.Id.ToString());
                Wrapped(permit.CharacterName + " · " + permit.GroupName);
                if (communityGroups.Any(group => group.Id == permit.GroupId && group.OwnerCharacterId == session!.Character.Id))
                {
                    if (ActionButton("Release character")) Mutate("DELETE", PermitsPath + "/" + permit.Id, null);
                    DrawPermitTransferDraft(permit);
                }
                ImGui.PopID();
            }
        }
    }

    private bool CanBuyPermitForGroup(GroupProfile group) =>
        group.OwnerCharacterId == session?.Character.Id && CanGroupAction(group, GroupCapabilities.TreasuryWithdraw);

    private bool DrawPermitBuyer(GroupProfile[] groups)
    {
        if (groups.Length == 1) permitBuyerGroup = groups[0].Id;
        var selected = groups.FirstOrDefault(x => x.Id == permitBuyerGroup);
        if (groups.Length > 1 && ImGui.BeginCombo("Buying group", selected?.Name ?? "Choose a group"))
        {
            foreach (var group in groups)
                if (ImGui.Selectable(group.Name.Replace("##", "# #") + "###permitGroup" + group.Id, permitBuyerGroup == group.Id))
                    permitBuyerGroup = group.Id;
            ImGui.EndCombo();
        }
        return groups.Any(x => x.Id == permitBuyerGroup);
    }

    private void DrawPermitListings(GroupProfile[] groups)
    {
        var playerId = session!.Character.Id;
        var mine = permitShop!.Listings.FirstOrDefault(x => x.CharacterId == playerId);
        if (mine != null)
        {
            ImGui.TextUnformatted($"Your listing · {mine.Total} {community!.Currency.Name}");
            if (mine.Status == "pending" && permitShop.Policy.Enabled)
            {
                if (ActionButton("Allow listing"))
                    Mutate("PUT", PermitsPath + "/listings/mine", new PermitListingConsent(true, mine.Total), mine.Etag);
                ImGui.SameLine();
            }
            if (ActionButton(mine.Status == "pending" ? "Decline" : "Withdraw listing"))
                Mutate("PUT", PermitsPath + "/listings/mine", new PermitListingConsent(false), mine.Etag);
            ImGui.Spacing();
        }

        var listings = permitShop.Listings.Where(x => x.Status == "listed" && permitShop.Policy.Enabled).ToArray();
        if (listings.Length == 0)
        {
            ImGui.TextDisabled(permitShop.Policy.Enabled ? "No active listings." : "The shop is closed.");
            return;
        }

        var hasBuyer = DrawPermitBuyer(groups);
        if (groups.Length == 0) ImGui.TextDisabled("Purchases use a group treasury and require a group owner.");
        if (!ImGui.BeginTable("PermitListings", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH)) return;
        ImGui.TableSetupColumn("Slave name", ImGuiTableColumnFlags.WidthStretch, 0.55f);
        ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthStretch, 0.45f);
        ImGui.TableHeadersRow();
        foreach (var listing in listings)
        {
            ImGui.PushID(listing.CharacterId.ToString());
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(listing.CharacterName);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{listing.Total} {community!.Currency.Name}");
            if (listing.CharacterId != playerId && groups.Length > 0)
            {
                ImGui.SameLine();
                ImGui.BeginDisabled(!hasBuyer);
                if (ActionButton("Buy") && permitBuyerGroup is { } groupId)
                    Mutate("POST", PermitsPath + "/purchases", new PermitPurchase(listing.CharacterId, groupId, listing.Total), listing.Etag);
                ImGui.EndDisabled();
            }
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawPrivatePermitDraft(GroupProfile[] groups)
    {
        if (privatePermitMember is not { } target || !CanOfferPrivatePermit || !permitShop!.Policy.Enabled) return;
        ImGui.Separator();
        ImGui.TextUnformatted($"Private offer to {target.Character.DisplayName}");
        Wrapped($"{permitShop.Policy.BasePrice} {community!.Currency.Name} plus outstanding debt, paid from the group treasury when accepted.");
        ImGui.PushID("PrivatePermitBuyer");
        var hasBuyer = DrawPermitBuyer(groups);
        ImGui.PopID();
        if (hasBuyer && permitBuyerGroup is { } groupId && ActionButton("Send offer"))
            Mutate("POST", PermitsPath + "/offers", new PermitOfferCreate(target.Character.Id, groupId, permitShop.Policy.BasePrice));
    }

    private void DrawPermitOffers()
    {
        if (permitShop!.Offers.Length == 0) return;
        ImGui.Separator();
        ImGui.TextUnformatted("Private offers");
        foreach (var offer in permitShop.Offers)
        {
            ImGui.PushID(offer.Id.ToString());
            var incoming = offer.CharacterId == session!.Character.Id;
            ImGui.TextUnformatted(incoming
                ? $"{offer.BuyerName} · {offer.GroupName}"
                : $"{offer.CharacterName} · Awaiting acceptance");
            var endpoint = PermitsPath + "/offers/" + offer.Id;
            if (incoming)
            {
                ImGui.TextUnformatted($"Price for the buyer: {offer.Total} {community!.Currency.Name}");
                Wrapped($"To the buyer: {offer.AssetsCurrency} {community.Currency.Name}");
                foreach (var asset in offer.Assets) Wrapped($"{asset.Quantity} × {asset.Name}");
                if (permitShop.Policy.Enabled)
                {
                    if (ActionButton("Accept")) Mutate("PUT", endpoint + "/accept", new PermitQuoteAction(offer.Quote));
                    ImGui.SameLine();
                }
            }
            if (ActionButton(incoming ? "Decline" : "Cancel offer"))
                Mutate("DELETE", endpoint, null);
            ImGui.Separator();
            ImGui.PopID();
        }
    }

    private void ApplyPermitSuccess(CommandRequest request, string json)
    {
        var purchased = request.Operation.Kind is CommandKind.BuyPermit or CommandKind.ConsentPermitOffer ? ReadCommand<OwnershipPermit>(json) : null;
        permitFeedback = request.Operation.Kind switch
        {
            CommandKind.NominatePermit => "Listing request sent.",
            CommandKind.BuyPermit => "Purchased " + purchased!.CharacterName + ".",
            CommandKind.CreatePermitOffer => "Registration offer sent.",
            CommandKind.ConsentPermitOffer => "Private offer completed.",
            CommandKind.WithdrawPermitOffer => "Offer closed.",
            CommandKind.ReleasePermit => "The character was released.",
            CommandKind.OfferPermitTransfer => "Transfer offer sent.",
            CommandKind.ApprovePermitTransfer => "Transfer approved.",
            CommandKind.AcceptPermitTransfer => "Permit transferred.",
            CommandKind.CancelPermitTransfer => "Transfer offer closed.",
            _ => "Permit changes saved."
        };
        if (purchased is { } permit && permitShop != null)
        {
            permitShop = permitShop with
            {
                Offers = permitShop.Offers.Where(offer => offer.CharacterId != permit.CharacterId).ToArray(),
                Listings = permitShop.Listings.Where(listing => listing.CharacterId != permit.CharacterId).ToArray(),
                Permits = permitShop.Permits.Where(x => x.Id != permit.Id).Append(permit).ToArray()
            };
        }
        else if (request.Operation.Kind == CommandKind.OfferPermitTransfer) transferPermit = null;
        else if (request.Operation.Kind == CommandKind.CreatePermitOffer)
        {
            privatePermitMember = null;
            privatePermitTarget = null;
        }
        else if (request.Operation.Kind == CommandKind.EditPermitPolicy)
        {
            var policy = ReadCommand<PermitPolicy>(json);
            permitBasePrice = policy.BasePrice;
            permitEnabled = policy.Enabled;
        }
        permitsRefresh.Invalidate();
    }
}
