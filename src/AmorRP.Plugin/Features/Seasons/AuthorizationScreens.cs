using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private Guid? authorizationResultFor, pendingAuthorizationRead;
    private string authorizationResult = "", authorizationDiagnostic = "";

    private static bool IsAuthorizationCommand(CommandRequest request) => request.Operation.Kind == CommandKind.EditMemberPermissions;

    private static string Permission(bool allowed) => allowed ? "Allowed" : "Not authorized";

    private void DrawAuthorizationFeedback(Member target)
    {
        if (authorizationResultFor != target.Character.Id) return;
        if (authorizationResult.Length > 0) Wrapped(authorizationResult);
        if (authorizationDiagnostic.Length > 0 && ImGui.CollapsingHeader("Authorization troubleshooting")) Wrapped(authorizationDiagnostic);
    }

    private void ReloadSavedPermissions(Member target)
    {
        var uri = origin!; var token = session!.AccessToken; var communityId = community!.Id; var characterId = target.Character.Id;
        pendingAuthorizationRead = authorizationResultFor = characterId;
        authorizationResult = "Checking this member's saved permissions with the server…"; authorizationDiagnostic = "";
        Start(async ct => {
            var current = await client.GetAsync<Member>(uri, $"api/v2/seasons/{communityId}/members/{characterId}", token, ct);
            return () => {
                pendingAuthorizationRead = null;
                if (community?.Id != communityId || memberSelection != characterId) return;
                AcceptSavedPermissions(current);
                authorizationResult = "Permissions reloaded.";
                status = authorizationResult;
            };
        }, "Checking saved permissions…");
    }

    private void AcceptSavedPermissions(Member current)
    {
        members = members.Select(x => x.Character.Id == current.Character.Id ? current : x).ToArray();
        memberDraftEtag = current.Etag;
        blockedGameplayGrants.Clear();
        blockedGameplayGrants.UnionWith(current.BlockedGameplayGrants ?? []);
        grantGathering = current.Capabilities.Contains(AmorRP.Core.Items.CraftingTypes.GatherPermission);
        grantBlacksmithing = current.Capabilities.Contains(AmorRP.Core.Items.CraftingTypes.BlacksmithPermission);
        grantBlacksmithDefinitions = current.Capabilities.Contains(AmorRP.Core.Items.CraftingTypes.BlacksmithRecipePermission);
        combatAllowed = !current.CombatRestricted;
        grantCooking = current.Capabilities.Contains("items.cooking.create"); grantCookingDefinitions = current.Capabilities.Contains("items.cooking.define");
        grantCoins = current.Capabilities.Contains("currency.manage");
        grantPotions = current.Capabilities.Contains("items.potion.create");
        grantRemoval = current.Capabilities.Contains("inventory.remove");
        tradingAllowed = !current.TradingRestricted;
        grantDefinitions = current.Capabilities.Contains("items.potion.define");
        grantGeneralModeration = current.Capabilities.Contains("season.moderate");
        grantAnnouncements = current.Capabilities.Contains("announcements.manage");
        grantGameplayPermissions = current.Capabilities.Contains("members.gameplay.manage");
        grantPermitListings = current.Capabilities.Contains(ManagePermitListings);
        grantGroupCreation = current.Capabilities.Contains("groups.create"); grantGroupManagement = current.Capabilities.Contains("groups.manage");
    }

    private void AcceptAuthorizationSave(CommandRequest request, CommandResult<Member> response)
    {
        if (!IsAuthorizationCommand(request)) return;
        var target = response.Result;
        authorizationResultFor = target.Character.Id;
        authorizationResult = "Permissions saved.";
        authorizationDiagnostic = "Committed operation: " + response.OperationId;
        if (memberSelection == target.Character.Id) memberDraftEtag = target.Etag;
        actionResult = authorizationResult;
    }

    private void NoteAuthorizationFailure(CommandRequest request, Exception? failure)
    {
        if (!IsAuthorizationCommand(request)) return;
        if (Guid.TryParse(request.Path.Split('/')[^2], out var characterId)) authorizationResultFor = characterId;
        var definitive = failure is FoundationApiException api && !api.OutcomeUncertain
            && (int)api.Status is >= 400 and < 500 && api.Status != System.Net.HttpStatusCode.TooManyRequests
            && api.Code != "operation_replay_expired";
        var uncertain = !definitive;
        authorizationResult = (uncertain ? "Save outcome is unresolved. Keep the saved action and check its receipt. " : "Permissions were not saved. ") + status;
        authorizationDiagnostic = actionDiagnostic;
    }

    private void NoteAuthorizationReceipt(CommandRequest request, Guid operationId)
    {
        if (!IsAuthorizationCommand(request)) return;
        if (Guid.TryParse(request.Path.Split('/')[^2], out var characterId)) authorizationResultFor = characterId;
        authorizationResult = "The server confirms this save committed. Reload saved permissions to check current values.";
        authorizationDiagnostic = "Committed operation: " + operationId;
    }

    private void NoteAuthorizationReadFailure(Exception? failure)
    {
        var details = failure is FoundationApiException api ? $" HTTP {(int)api.Status}; code: {api.Code}; request: {api.RequestId ?? "unavailable"}." : "";
        if (pendingAuthorizationRead.HasValue)
        {
            authorizationResultFor = pendingAuthorizationRead; pendingAuthorizationRead = null;
            authorizationResult = "Could not check saved permissions. " + status;
            authorizationDiagnostic = details;
        }
    }

    private void ClearAuthorizationViews()
    {
        authorizationResultFor = pendingAuthorizationRead = null;
        authorizationResult = authorizationDiagnostic = "";
    }
}
