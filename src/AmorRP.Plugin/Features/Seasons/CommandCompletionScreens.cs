using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Plugin.Services.Api;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private void ApplyCommandSuccess(CommandRequest request, CommandResponse response, bool recovering)
    {
        if (request.Operation.Kind == CommandKind.MoveGroupStock)
        {
            groupStorageRefresh.Invalidate();
            groupDepositRefresh.Invalidate();
        }
        if (request.Operation.Kind is CommandKind.CreateGroupShift or CommandKind.CancelGroupShift) groupShiftsRefresh.Invalidate();
        if (request.Operation.Kind == CommandKind.SetSeasonCreationPermission) seasonCreationAccessLoaded = false;
        // A recovered action may belong to another community. Its durable
        // receipt is still acknowledged, but must not overwrite this editor.
        if (!request.AppliesToSeason(community?.Id)) return;
        if (request.Operation.Kind is CommandKind.FavoriteGroup or CommandKind.UnfavoriteGroup)
        {
            var groupId = request.TargetGroupId();
            var favorite = request.Operation.Kind == CommandKind.FavoriteGroup;
            communityGroups = communityGroups.Select(group => group.Id == groupId ? group with { IsFavorite = favorite } : group).ToArray();
            return;
        }
        if (request.Operation.Handler == CommandHandler.Billing)
        {
            ApplyBillingSuccess(response.Json);
            return;
        }
        if (request.Operation.Handler is CommandHandler.GroupProfile or CommandHandler.GroupActivity or CommandHandler.Treasury or CommandHandler.GroupBilling
            && !request.AppliesToGroup(selectedGroupId)) return;
        switch (request.Operation.Handler)
        {
            case CommandHandler.SeasonForm: ApplyFormSuccess(request, response.Json); break;
            case CommandHandler.Gathering: ApplyGatheringSuccess(request, response.Json); break;
            case CommandHandler.Permit: ApplyPermitSuccess(request, response.Json); break;
            case CommandHandler.Discord: ApplyDiscordSuccess(request, response.Json, recovering); break;
            case CommandHandler.Shop: ApplyShopSuccess(request, response.Json); break;
            case CommandHandler.Home: ApplyHomeSuccess(request, response.Json); break;
            case CommandHandler.GroupProfile: ApplyGroupProfileSuccess(request, response.Json); break;
            case CommandHandler.GroupActivity: ApplyGroupActivitySuccess(request); break;
            case CommandHandler.GroupBilling: ApplyGroupBillingSuccess(request, response.Json); break;
            case CommandHandler.BillingPolicy: billingPolicyEtag = ""; AcceptBillingPolicy(ReadCommand<BillingPolicy>(response.Json)); billingPolicyRefresh.Invalidate(); break;
            case CommandHandler.Treasury: ApplyTreasurySuccess(response.Json); break;
            case CommandHandler.Inventory: ApplyInventorySuccess(request, response, recovering); break;
            case CommandHandler.Trade: ApplyTradeSuccess(request, response.Json); break;
            case CommandHandler.Balance:
                var balance = ReadCommand<Balance>(response.Json);
                if (memberSelection == balance.CharacterId && community?.Currency.Id == balance.CurrencyId)
                { selectedMemberBalance = balance; balanceFor = balance.CharacterId; }
                break;
            case CommandHandler.Member:
                var updated = JsonSerializer.Deserialize<CommandResult<Member>>(response.Json, FoundationClient.Json)!;
                members = members.Select(x => x.Character.Id == updated.Result.Character.Id ? updated.Result : x).ToArray();
                memberSearchResults = memberSearchResults.Select(x => x.Character.Id == updated.Result.Character.Id ? updated.Result : x).ToArray();
                if (memberSelection == updated.Result.Character.Id) memberDraftEtag = updated.Result.Etag;
                AcceptAuthorizationSave(request, updated); AcceptMemberDescriptionSave(request, updated.Result);
                break;
            case CommandHandler.Policy: AcceptPolicySave(ReadCommand<SeasonPolicies>(response.Json)); break;
        }
    }

    private static T ReadCommand<T>(string json) => JsonSerializer.Deserialize<CommandResult<T>>(json, FoundationClient.Json)!.Result;
}
