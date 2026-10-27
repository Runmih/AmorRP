using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private bool policyDraftLoaded;
    private readonly FileDialogManager files = new();
    private string iconPath = "";
    private void SwitchCommunity(Community entry)
    {
        if (TradeBlocksCommunitySwitch && community?.Id != entry.Id) { switchAfterTrade = entry; OpenTradeWindow?.Invoke(); actionResult = "End the current trade before switching communities."; return; }
        ClearWork(); ClearCommunityViews(); selected = entry.Id; community = entry; refreshNeeded = true;
        if (saved != null) { saved.SelectedCommunityId = selected; save(); }
    }
    private void ClearCommunityViews()
    {
        ResetCommunityRefreshes();
        ClearNestedGroups();
        requests.ChangeView(); reads.Clear(); ClearTradeViews();
        icons.Clear(); logos.Clear(); community = null; members = []; policies = null; transfer = null; invitations = [];
        ClearMemberDescription(); memberSelection = null; memberDraftEtag = null; memberSearch = ""; policyDraftVersion = editCommunityVersion = editCurrencyVersion = 0; membersCursor = invitationsCursor = null;
        createdCode = ""; policyDraftLoaded = false; showInactive = false;
        ClearHomeViews(); editDescription = editCurrencyName = ""; iconPath = "";
        ClearMemberBalance();
        removalReason = reason = delta = ""; tradingAllowed = true; grantDefinitions = false;
        ClearInventoryViews();
        ClearAuthorizationViews();
    }
    private void ApplyFormSuccess(CommandRequest request, string? response)
    {
        if (request.Operation.Kind == CommandKind.JoinCommunity) { invitationCode = ""; visibilityAccepted = false; CloseJoinCommunity?.Invoke(); }
        if (request.Operation.Kind == CommandKind.CreateCommunity)
        {
            name = description = ""; currencyName = "Coins"; symbol = "C";
            initialCooking = 0;
            initialPoints = serviceLimits?.DefaultPotionPoints ?? 0; initialLetters = serviceLimits?.DefaultLettersPerWeek ?? 5;
            initialAllowancesEdited = false;
            CloseCreateCommunity?.Invoke();
        }
        if (request.Operation.Handler == CommandHandler.CommunityForm && response != null)
            selected = JsonSerializer.Deserialize<CommandResult<Community>>(response, FoundationClient.Json)!.Result.Id;
    }
    private void DrawCurrencyIconEditor()
    {
        Wrapped("Choose a static PNG, JPG or WebP, at most 128 pixels wide and high. Images are stored with this community.");
        icons.DrawIcon();
        if (ActionButton("Choose image…", mutation: false)) files.OpenFileDialog("Currency icon", ".png,.jpg,.jpeg,.webp", (ok, path) => { if (ok) iconPath = path; });
        if (iconPath.Length > 0)
        {
            Wrapped("Selected: " + Path.GetFileName(iconPath));
            if (ActionButton("Upload currency icon"))
            {
                var path = iconPath; var endpoint = $"api/v2/communities/{community!.Id}/currency/icon"; var etag = ETag("currency", community.Currency.Id, community.Currency.Version);
                Start(async ct => {
                    var maximum = serviceLimits?.MaxCurrencyIconBytes ?? 262144;
                    await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    if (file.Length > maximum || file.Length <= 0) throw new InvalidOperationException($"Choose an image smaller than {maximum / 1024} KiB.");
                    var bytes = new byte[(int)file.Length]; await file.ReadExactlyAsync(bytes, ct);
                    return () => QueueUpload(endpoint, etag, bytes);
                }, "Reading selected image…");
            }
        }
        if (community!.Currency.IconAssetId != null && ActionButton("Use default currency icon")) Mutate("DELETE", $"api/v2/communities/{community.Id}/currency/icon", null, ETag("currency", community.Currency.Id, community.Currency.Version));
    }
    private void QueueUpload(string path, string etag, byte[] bytes)
    {
        var request = new CommandRequest("PUT", path, null, etag, Convert.ToBase64String(bytes));
        var pending = new PendingCommand { Origin = origin!.AbsoluteUri, CharacterId = session!.Character.Id, Key = Guid.CreateVersion7(), ProtectedRequest = PendingCommand.Protect(request) };
        configuration.PendingCommands.Add(pending); save(); ExecutePending(pending, false);
    }
}
