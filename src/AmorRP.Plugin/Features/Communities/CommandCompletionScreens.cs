using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Plugin.Services.Api;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
{
    private void ApplyCommandSuccess(CommandRequest request, CommandResponse response, bool recovering)
    {
        if (request.Operation.Kind == CommandKind.CollectTreasuryReturn) ClearTreasuryReturns();
        // A recovered action may belong to another community. Its durable
        // receipt is still acknowledged, but must not overwrite this editor.
        if (!request.AppliesToCommunity(community?.Id)) return;
        if (request.Operation.Handler is CommandHandler.GroupProfile or CommandHandler.GroupActivity or CommandHandler.Treasury
            && !request.AppliesToGroup(selectedGroupId)) return;
        switch (request.Operation.Handler)
        {
            case CommandHandler.CommunityForm: ApplyFormSuccess(request, response.Json); break;
            case CommandHandler.Home: ApplyHomeSuccess(request, response.Json); break;
            case CommandHandler.GroupProfile: ApplyGroupProfileSuccess(request, response.Json); break;
            case CommandHandler.GroupActivity: ApplyGroupActivitySuccess(request); break;
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
            case CommandHandler.Policy: AcceptPolicySave(request, ReadCommand<CommunityPolicies>(response.Json)); break;
            case CommandHandler.Invitation: createdCode = ReadCommand<InvitationCreated>(response.Json).InvitationCode; break;
        }
    }

    private static T ReadCommand<T>(string json) => JsonSerializer.Deserialize<CommandResult<T>>(json, FoundationClient.Json)!.Result;
}
