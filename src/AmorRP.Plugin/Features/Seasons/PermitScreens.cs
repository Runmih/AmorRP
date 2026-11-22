using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Permits;
using AmorRP.Contracts.Seasons;
using AmorRP.Plugin.Features.Shell;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private const string ManagePermitListings = "permits.list.manage";
    private readonly BackgroundRefresh<PermitShop> permitsRefresh = new(TimeSpan.FromSeconds(12));
    private PermitShop? permitShop;
    private string permitBasePrice = "", permitFeedback = "";
    private bool permitEnabled, permitTabRequested;
    private Guid? permitBuyerGroup, permitTargetId, permitReviewId;
    private CharacterTarget? privatePermitTarget;
    private Member? privatePermitMember;
    private bool CanNominatePermit => Owner || community?.MyCapabilities.Contains(ManagePermitListings) == true;
    public bool CanOfferPrivatePermit => session != null && community != null && !SeasonReadOnly
        && communityGroups.Any(group => group.OwnerCharacterId == session.Character.Id && CanGroupAction(group, GroupCapabilities.TreasuryWithdraw));

    private string PermitsPath => $"api/v2/seasons/{community!.Id}/permits";

    private void ClearPermits()
    {
        permitsRefresh.Reset();
        permitShop = null;
        permitBasePrice = permitFeedback = "";
        permitEnabled = permitTabRequested = false;
        permitBuyerGroup = permitTargetId = permitReviewId = null;
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
        permitTargetId = permitReviewId = null;
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
        if (permitShop.Permits.Length > 0 && ImGui.CollapsingHeader("Registered permits", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var permit in permitShop.Permits)
            {
                ImGui.PushID(permit.Id.ToString());
                Wrapped(permit.CharacterName + " · " + permit.GroupName);
                if (communityGroups.Any(group => group.Id == permit.GroupId && group.OwnerCharacterId == session!.Character.Id)
                    && ActionButton("Release character"))
                    Mutate("DELETE", PermitsPath + "/" + permit.Id, null);
                ImGui.PopID();
            }
        }
        DrawPermitOffers();
        DrawPermitListings();
    }

    private void DrawPermitListings()
    {
        var playerId = session!.Character.Id;
        var mine = permitShop!.Listings.FirstOrDefault(x => x.CharacterId == playerId);
        if (mine != null)
        {
            ImGui.Separator();
            Wrapped($"Your listing: {mine.CharacterName} · {mine.Total} {community!.Currency.Name}");
            if (mine.Status == "pending")
            {
                Wrapped("Publishing is optional. It makes this character name and price visible in the Season Shop.");
                if (permitShop.Policy.Enabled && ActionButton("I consent OOC — publish my listing"))
                    Mutate("PUT", PermitsPath + "/listings/mine", new PermitListingConsent(true, mine.Total), mine.Etag);
            }
            if (ActionButton(mine.Status == "pending" ? "Decline listing" : "Withdraw my listing"))
                Mutate("PUT", PermitsPath + "/listings/mine", new PermitListingConsent(false), mine.Etag);
        }
        if (!permitShop.Policy.Enabled) return;
        ImGui.Separator();
        foreach (var listing in permitShop.Listings.Where(x => x.Status == "listed" && x.CharacterId != playerId))
        {
            if (!ImGui.Selectable($"{listing.CharacterName} · {listing.Total} {community!.Currency.Name}###permit{listing.CharacterId}", listing.CharacterId == permitTargetId)) continue;
            permitTargetId = listing.CharacterId;
            privatePermitTarget = null;
            privatePermitMember = null;
            permitFeedback = "";
        }
        var target = privatePermitMember?.Character.Id ?? permitTargetId;
        if (target == null || !CanOfferPrivatePermit) return;
        var name = privatePermitMember?.Character.DisplayName ?? permitShop.Listings.FirstOrDefault(x => x.CharacterId == target)?.CharacterName;
        if (name == null) return;
        Wrapped(privatePermitMember != null ? $"Private offer to {name}" : $"Request registration for {name}");
        var groups = communityGroups.Where(group => group.OwnerCharacterId == playerId && CanGroupAction(group, GroupCapabilities.TreasuryWithdraw)).ToArray();
        if (groups.Length == 1) permitBuyerGroup = groups[0].Id;
        if (ImGui.BeginCombo("Owning group", groups.FirstOrDefault(x => x.Id == permitBuyerGroup)?.Name ?? "Choose a group"))
        {
            foreach (var group in groups)
                if (ImGui.Selectable(group.Name + "###permitGroup" + group.Id, permitBuyerGroup == group.Id)) permitBuyerGroup = group.Id;
            ImGui.EndCombo();
        }
        if (permitBuyerGroup is { } groupId && groups.Any(x => x.Id == groupId) && ActionButton("Send offer"))
            Mutate("POST", PermitsPath + "/offers", new PermitOfferCreate(target.Value, groupId, privatePermitMember != null));
    }

    private void DrawPermitOffers()
    {
        if (permitShop!.Offers.Length == 0) return;
        ImGui.Separator();
        ImGui.TextUnformatted("Registration offers");
        foreach (var offer in permitShop.Offers)
        {
            ImGui.PushID(offer.Id.ToString());
            if (ImGui.Selectable($"{offer.CharacterName} · {offer.GroupName}###Offer", permitReviewId == offer.Id)) permitReviewId = offer.Id;
            ImGui.PopID();
        }
        if (permitShop.Offers.FirstOrDefault(x => x.Id == permitReviewId) is not { } reviewing) return;
        Wrapped($"Buyer: {reviewing.BuyerName} · Owning group: {reviewing.GroupName}");
        Wrapped($"Permit: {reviewing.BasePrice} · Debt: {reviewing.Debt} · Total: {reviewing.Total} {community!.Currency.Name}");
        Wrapped("Registration locks Trusted billing on for the owning group until release.");
        if (reviewing.Private)
        {
            Wrapped($"Assets to the buyer: {reviewing.AssetsCurrency} {community.Currency.Name}");
            foreach (var asset in reviewing.Assets) Wrapped($"{asset.Quantity} × {asset.Name}");
        }
        var endpoint = PermitsPath + "/offers/" + reviewing.Id;
        if (reviewing.CharacterId == session!.Character.Id && !reviewing.Accepted && permitShop.Policy.Enabled && ActionButton("I consent OOC — accept this offer"))
            Mutate("PUT", endpoint + "/consent", new PermitQuoteAction(reviewing.Quote));
        if (reviewing.GroupBalance is { } balance && Int128.TryParse(balance, out var available) && Int128.TryParse(reviewing.Total, out var total))
        {
            Wrapped($"Group treasury: {balance}");
            if (available < total) Wrapped($"Shortfall: {total - available}");
            if (!reviewing.Accepted) Wrapped("Waiting for the player to accept this exact quote.");
            if (reviewing.Accepted && available >= total && permitShop.Policy.Enabled && ActionButton("Pay and register"))
                Mutate("POST", endpoint + "/register", new PermitQuoteAction(reviewing.Quote));
        }
        else if (reviewing.Accepted) Wrapped("Accepted. Waiting for the group owner to register.");
        if (ActionButton("Withdraw offer")) Mutate("DELETE", endpoint, null);
    }

    private void ApplyPermitSuccess(CommandRequest request, string json)
    {
        permitFeedback = request.Operation.Kind switch
        {
            CommandKind.NominatePermit => "Private listing consent request sent.",
            CommandKind.CreatePermitOffer => "Registration offer sent.",
            CommandKind.RegisterPermit => "The named permit was registered.",
            CommandKind.ReleasePermit => "The character was released.",
            _ => "Permit changes saved."
        };
        if (request.Operation.Kind == CommandKind.CreatePermitOffer)
        {
            permitReviewId = ReadCommand<PermitOffer>(json).Id;
            permitTargetId = null;
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
