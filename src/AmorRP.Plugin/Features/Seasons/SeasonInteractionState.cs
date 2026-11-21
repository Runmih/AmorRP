using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private bool policyDraftLoaded;
    private readonly FileDialogManager files = new();
    private string iconPath = "";
    private void SwitchCommunity(Season entry)
    {
        if (TradeBlocksCommunitySwitch && community?.Id != entry.Id) { switchAfterTrade = entry; OpenTradeWindow?.Invoke(); actionResult = "End the current trade before switching seasons."; return; }
        ClearWork(); ClearCommunityViews(); selected = entry.Id; community = entry; refreshNeeded = true;
        if (saved != null) { saved.SelectedCommunityId = selected; save(); }
    }
    private void ClearCommunityViews()
    {
        pendingMemberSelection = null; requestedCommunityPage = null; requestedManagementMembers = false; selectedEventId = null;
        ResetCommunityRefreshes();
        ClearNestedGroups();
        ClearContextBilling();
        requests.ChangeView(); reads.Clear(); ClearTradeViews();
        icons.Clear(); logos.Clear(); community = null; members = []; policies = null; transfer = null;
        ClearMemberDescription(); memberSelection = null; memberDraftEtag = null; memberSearch = ""; policyDraftVersion = editCommunityVersion = editCurrencyVersion = 0; membersCursor = null;
        policyDraftLoaded = false;
        ClearHomeViews(); editDescription = editCurrencyName = ""; iconPath = "";
        ClearMemberBalance();
        reason = delta = ""; tradingAllowed = true; grantDefinitions = false;
        ClearInventoryViews();
        ClearAuthorizationViews();
    }
    private void ApplyFormSuccess(CommandRequest request, string? response)
    {
        if (request.Operation.Kind == CommandKind.CreateSeason)
        {
            name = description = ""; currencyName = "Coins"; symbol = "C";
            initialCooking = 0;
            initialPoints = serviceLimits?.DefaultPotionPoints ?? 0; initialLetters = serviceLimits?.DefaultLettersPerWeek ?? 5;
            initialAllowancesEdited = false;
            CloseCreateCommunity?.Invoke();
        }
        if (request.Operation.Handler == CommandHandler.SeasonForm && response != null)
            selected = JsonSerializer.Deserialize<CommandResult<Season>>(response, FoundationClient.Json)!.Result.Id;
    }
    private void DrawCurrencyIconEditor()
    {
        Wrapped("Choose a static PNG, JPG or WebP, at most 128 pixels wide and high. Images are stored with this season.");
        icons.DrawIcon();
        if (ActionButton("Choose image…", mutation: false)) files.OpenFileDialog("Currency icon", ".png,.jpg,.jpeg,.webp", (ok, path) => { if (ok) iconPath = path; });
        if (iconPath.Length > 0)
        {
            Wrapped("Selected: " + Path.GetFileName(iconPath));
            if (ActionButton("Upload currency icon"))
            {
                var path = iconPath; var endpoint = $"api/v2/seasons/{community!.Id}/currency/icon"; var etag = ETag("currency", community.Currency.Id, community.Currency.Version);
                Start(async ct => {
                    var maximum = serviceLimits?.MaxCurrencyIconBytes ?? 262144;
                    await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    if (file.Length > maximum || file.Length <= 0) throw new InvalidOperationException($"Choose an image smaller than {maximum / 1024} KiB.");
                    var bytes = new byte[(int)file.Length]; await file.ReadExactlyAsync(bytes, ct);
                    return () => QueueUpload(endpoint, etag, bytes);
                }, "Reading selected image…");
            }
        }
        if (community!.Currency.IconAssetId != null && ActionButton("Use default currency icon")) Mutate("DELETE", $"api/v2/seasons/{community.Id}/currency/icon", null, ETag("currency", community.Currency.Id, community.Currency.Version));
    }
    private void QueueUpload(string path, string etag, byte[] bytes)
    {
        if (WriteBlocked || SeasonReadOnly) return;
        var request = new CommandRequest("PUT", path, null, etag, Convert.ToBase64String(bytes));
        var pending = new PendingCommand { Origin = origin!.AbsoluteUri, CharacterId = session!.Character.Id, Key = Guid.CreateVersion7(), ProtectedRequest = PendingCommand.Protect(request) };
        configuration.PendingCommands.Add(pending); save(); ExecutePending(pending, false);
    }
}
