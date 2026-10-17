using System.Net;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private static readonly TimeSpan FeatureInterval = TimeSpan.FromMinutes(1);
    private readonly BackgroundRefresh<InventoryState> inventoryRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<MemberPage> rosterRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<CommunityPolicies> policyRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<InvitationPage> invitationRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<OwnershipTransfer?> ownershipRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<GroupProfile[]> groupsRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<GroupHub> hubRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<MemberInventoryState> inspectorRefresh = new(FeatureInterval);
    private readonly BackgroundRefresh<ImageRead> iconRefresh = new(FeatureInterval), logoRefresh = new(FeatureInterval);
    private string iconReadKey = "", logoReadKey = "";
    private sealed record ImageRead(string Key, byte[]? Bytes, bool Custom);
    private sealed record MemberInventoryState(Guid CharacterId, HoldingPage Page);

    private void ResetCommunityRefreshes()
    {
        inventoryRefresh.Reset(); rosterRefresh.Reset(); policyRefresh.Reset(); invitationRefresh.Reset();
        ownershipRefresh.Reset(); groupsRefresh.Reset(); hubRefresh.Reset(); inspectorRefresh.Reset();
        iconRefresh.Reset(); logoRefresh.Reset(); iconReadKey = logoReadKey = "";
    }

    private void RefreshCommunityFeatures()
    {
        inventoryRefresh.Invalidate(); rosterRefresh.Invalidate(); policyRefresh.Invalidate();
        invitationRefresh.Invalidate(); ownershipRefresh.Invalidate(); groupsRefresh.Invalidate();
        hubRefresh.Invalidate(); inspectorRefresh.Invalidate();
    }

    private void TickFeatureRefreshes()
    {
        var now = DateTimeOffset.UtcNow;
        if (inventoryRefresh.TryCollect(now, dataGeneration, out var inventory)) AcceptInventoryState(inventory);
        if (rosterRefresh.TryCollect(now, dataGeneration, out var roster))
        {
            members = roster.Items; membersCursor = roster.NextCursor;
            if (!members.Concat(memberSearchResults).Any(x => x.Character.Id == memberSelection)) memberSelection = null;
        }
        if (policyRefresh.TryCollect(now, dataGeneration, out var policy))
        {
            policies = policy;
            if (!policyDraftLoaded) LoadActivePolicy(policy);
        }
        if (invitationRefresh.TryCollect(now, dataGeneration, out var invites))
        { invitations = invites.Items; invitationsCursor = invites.NextCursor; }
        if (ownershipRefresh.TryCollect(now, dataGeneration, out var ownership)) transfer = ownership;
        if (groupsRefresh.TryCollect(now, dataGeneration, out var groups)) AcceptCommunityGroups(groups);
        if (hubRefresh.TryCollect(now, dataGeneration, out var groupHub)) hub = groupHub;
        if (inspectorRefresh.TryCollect(now, dataGeneration, out var inspector) && memberInventoryFor == inspector.CharacterId)
        { memberHoldings = inspector.Page.Items; memberHoldingCursor = inspector.Page.NextCursor; }
        if (iconRefresh.TryCollect(now, dataGeneration, out var icon) && icon.Key == iconReadKey)
            icons.Update(icon.Key, icon.Bytes, icon.Custom);
        if (logoRefresh.TryCollect(now, dataGeneration, out var logo) && logo.Key == logoReadKey)
            logos.Update(logo.Key, logo.Bytes, logo.Custom);

        var errors = new[] { inventoryRefresh.Error, rosterRefresh.Error, policyRefresh.Error, invitationRefresh.Error,
            ownershipRefresh.Error, groupsRefresh.Error, hubRefresh.Error, inspectorRefresh.Error, iconRefresh.Error, logoRefresh.Error };
        if (!SessionRenewalInProgress && errors.Any(x => x is FoundationApiException { Status: HttpStatusCode.Unauthorized }))
        { ClearWork(); ClearCommunityViews(); session = null; return; }
        if (SessionRenewalInProgress || session == null || origin == null || community == null || WriteBlocked || session.AccessExpiresAt <= now) return;

        var uri = origin; var token = session.AccessToken; var active = community; var actor = session.Character.Id;
        var ct = lifetime.Token; var id = active.Id; var owner = active.OwnerCharacterId == actor;
        var query = InventoryQuery(); var holding = holdingSelection; var count = holdings.Length;
        inventoryRefresh.Start(now, dataGeneration, ct, c => LoadInventoryAsync(uri, token, id, query, holding, count, c));
        var rosterCount = members.Length; var selectedMember = memberSelection; var inactive = owner && showInactive;
        rosterRefresh.Start(now, dataGeneration, ct, c => LoadRosterAsync(uri, token, id, inactive, owner, rosterCount, selectedMember, c));
        policyRefresh.Start(now, dataGeneration, ct, c => client.GetAsync<CommunityPolicies>(uri, $"api/v2/communities/{id}/policies", token, c));
        if (owner) invitationRefresh.Start(now, dataGeneration, ct, c => client.GetAsync<InvitationPage>(uri, $"api/v2/communities/{id}/invitations", token, c));
        else { invitationRefresh.Reset(); invitations = []; invitationsCursor = null; }
        ownershipRefresh.Start(now, dataGeneration, ct, c => LoadOwnershipAsync(uri, token, active, c));
        groupsRefresh.Start(now, dataGeneration, ct, c => LoadCommunityGroupsAsync(uri, token, id, c));
        hubRefresh.Start(now, dataGeneration, ct, c => client.GetAsync<GroupHub>(uri, $"api/v2/communities/{id}/group-hub", token, c));
        if (memberInventoryFor is { } target && (owner || active.MyCapabilities.Contains("inventory.remove")))
        {
            var inspectorCount = memberHoldings.Length;
            inspectorRefresh.Start(now, dataGeneration, ct, c => LoadInspectorAsync(uri, token, id, target, inspectorCount, c));
        }
        else { inspectorRefresh.Reset(); memberHoldings = []; memberInventoryFor = null; memberHoldingSelection = null; }
        TickCommunityImages(uri, token, active, actor, now, ct);
    }

    private async Task<MemberPage> LoadRosterAsync(Uri uri, string token, Guid id, bool inactive, bool owner, int loadedCount, Guid? selection, CancellationToken ct)
    {
        var path = $"api/v2/communities/{id}/members";
        var suffix = inactive ? "&includeDormant=true" : "";
        var page = await client.GetAsync<MemberPage>(uri, path + (inactive ? "?includeDormant=true" : ""), token, ct);
        var rows = page.Items.ToList();
        while (page.NextCursor != null && rows.Count < loadedCount)
        {
            page = await client.GetAsync<MemberPage>(uri, path + "?cursor=" + Uri.EscapeDataString(page.NextCursor) + suffix, token, ct);
            rows.AddRange(page.Items);
        }
        if (selection.HasValue && rows.All(x => x.Character.Id != selection))
        {
            try
            {
                var selectedMember = await client.GetAsync<Member>(uri, path + "/" + selection, token, ct);
                if (selectedMember.Status == "active" || owner && inactive) rows.Add(selectedMember);
            }
            catch (FoundationApiException error) when (error.Status == HttpStatusCode.NotFound) { }
        }
        return page with { Items = rows.ToArray() };
    }

    private async Task<OwnershipTransfer?> LoadOwnershipAsync(Uri uri, string token, Community active, CancellationToken ct)
    {
        if (active.PendingOwnershipTransferId == null) return null;
        try { return await client.GetAsync<OwnershipTransfer>(uri, $"api/v2/communities/{active.Id}/ownership-transfers/{active.PendingOwnershipTransferId}", token, ct); }
        catch (FoundationApiException error) when (error.Status == HttpStatusCode.NotFound) { return null; }
    }

    private async Task<MemberInventoryState> LoadInspectorAsync(Uri uri, string token, Guid id, Guid target, int count, CancellationToken ct)
    {
        var path = $"api/v2/communities/{id}/members/{target}/holdings";
        try
        {
            var page = await client.GetAsync<HoldingPage>(uri, path, token, ct);
            var rows = page.Items.ToList();
            while (page.NextCursor != null && rows.Count < count)
            {
                page = await client.GetAsync<HoldingPage>(uri, path + "?cursor=" + Uri.EscapeDataString(page.NextCursor), token, ct);
                rows.AddRange(page.Items);
            }
            return new(target, page with { Items = rows.ToArray() });
        }
        catch (FoundationApiException error) when (error.Status is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        { return new(target, new([], null, "0")); }
    }

    private void TickCommunityImages(Uri uri, string token, Community active, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var iconKey = $"{uri}|{actor}|{active.Id}|{active.Currency.IconAssetId}";
        var logoKey = $"{uri}|{actor}|{active.Id}|{active.LogoAssetId}";
        if (iconReadKey != iconKey) { iconReadKey = iconKey; iconRefresh.Invalidate(); }
        if (logoReadKey != logoKey) { logoReadKey = logoKey; logoRefresh.Invalidate(); }
        if (!icons.Matches(iconKey))
            iconRefresh.Start(now, dataGeneration, ct, async c => new ImageRead(iconKey,
                active.Currency.IconAssetId == null ? null : await client.GetIconAsync(uri, $"api/v2/communities/{active.Id}/currency/icon", token, c), active.Currency.IconAssetId != null));
        if (!logos.Matches(logoKey))
            logoRefresh.Start(now, dataGeneration, ct, async c => new ImageRead(logoKey,
                active.LogoAssetId == null ? null : await client.GetImageAsync(uri, $"api/v2/communities/{active.Id}/logo", token, 1048576, c), active.LogoAssetId != null));
    }

    private static void DrawRefreshState<T>(BackgroundRefresh<T> refresh, string label)
    {
        if (refresh.Error != null)
        {
            Wrapped(label + " could not refresh. " + (refresh.HasLoaded ? "Showing the last update." : "Retrying shortly."));
            if (ImGui.SmallButton("Retry##" + label)) refresh.Invalidate();
            ImGui.SameLine();
            if (ImGui.SmallButton("Details##" + label)) ImGui.OpenPopup("RefreshDetails##" + label);
            ImGui.SetNextWindowSize(new(ImGui.GetFontSize() * 25, 0), ImGuiCond.Appearing);
            if (ImGui.BeginPopup("RefreshDetails##" + label))
            {
                Wrapped(UserFeedback.Details(refresh.Error));
                ImGui.EndPopup();
            }
        }
        else if (!refresh.HasLoaded) ImGui.TextDisabled("Loading " + label.ToLowerInvariant() + "…");
    }
}
