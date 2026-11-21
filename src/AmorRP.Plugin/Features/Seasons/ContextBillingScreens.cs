using System.Globalization;
using System.Text.Json;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Seasons;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Dalamud.Bindings.ImGui;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private sealed class ContextBillDraft(CharacterTarget target, Guid season, Guid actor)
    {
        public CharacterTarget Target { get; } = target;
        public Guid Season { get; } = season;
        public Guid Actor { get; } = actor;
        public Guid Id { get; } = Guid.CreateVersion7();
        public Guid? GroupId;
        public Member? Customer;
        public bool Recorded;
        public string Amount = "", Service = "", Feedback = "";
    }

    private readonly BackgroundRefresh<Member?> contextCustomerRefresh = new(TimeSpan.FromMinutes(1));
    private ContextBillDraft? contextBill;
    public bool CanOpenContextBilling => session != null && community != null && !SeasonReadOnly
        && communityGroups.Any(group => CanGroupAction(group, GroupCapabilities.Billing));

    public bool OpenContextBilling(CharacterTarget target)
    {
        if (!CanOpenContextBilling || community is not { } season || session is not { } actor) return false;

        if (contextBill is { Recorded: false } existing && existing.Target == target
            && existing.Season == season.Id && existing.Actor == actor.Character.Id) return true;

        ClearContextBilling();
        contextBill = new ContextBillDraft(target, season.Id, actor.Character.Id);
        return true;
    }

    public void ClearContextBilling()
    {
        contextCustomerRefresh.Reset();
        contextBill = null;
    }

    public void DrawContextBilling()
    {
        if (contextBill is not { } draft || session is not { } actor || origin is not { } uri
            || community is not { } season || draft.Season != season.Id || draft.Actor != actor.Character.Id || SeasonReadOnly)
        {
            Wrapped("Open Billing from a player’s context menu in the active season.");
            return;
        }

        Wrapped(draft.Customer is { } customer
            ? $"{customer.Character.DisplayName} @ {customer.Character.HomeWorldName}"
            : draft.Target.Name);
        if (draft.Feedback.Length > 0) Wrapped(draft.Feedback);
        if (draft.Recorded) return;

        var now = DateTimeOffset.UtcNow;
        if (contextCustomerRefresh.TryCollect(now, dataGeneration, out var match)) draft.Customer = match;
        if (!contextCustomerRefresh.HasLoaded && serviceCompatible && !SessionRenewalInProgress)
            contextCustomerRefresh.Start(now, dataGeneration, requests.Token,
                ct => FindSeasonCharacterAsync(uri, actor.AccessToken, season.Id, draft.Target, ct));
        if (!contextCustomerRefresh.HasLoaded)
        {
            DrawRefreshState(contextCustomerRefresh, "Customer");
            return;
        }
        if (draft.Customer is not { } recipient)
        {
            Wrapped("This character isn’t an active member of the selected season.");
            return;
        }

        var groups = communityGroups.Where(group => CanGroupAction(group, GroupCapabilities.Billing))
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        if (groups.Length == 0)
        {
            Wrapped("You don’t have billing access for any group in this season.");
            return;
        }
        if (WriteBlocked && HasPendingAction) Wrapped("Check the pending action in the Activity Log before charging again.");

        ImGui.BeginDisabled(WriteBlocked);
        var group = groups.FirstOrDefault(row => row.Id == draft.GroupId);
        if (groups.Length == 1)
        {
            group = groups[0];
            Wrapped(group.Name);
        }
        else
        {
            FieldLabel("Bill from");
            if (ImGui.BeginCombo("##ContextBillGroup", group?.Name ?? "Choose a group"))
            {
                foreach (var choice in groups)
                {
                    if (!ImGui.Selectable(choice.Name + "###contextBill" + choice.Id, group?.Id == choice.Id)) continue;
                    draft.GroupId = choice.Id;
                    group = choice;
                }
                ImGui.EndCombo();
            }
        }
        var amount = DrawBillFields(ref draft.Amount, ref draft.Service);
        ImGui.BeginDisabled(group == null || amount == null);
        if (ActionButton("Charge customer") && group != null && amount != null)
        {
            draft.Feedback = "Saving bill…";
            Mutate("POST", GroupPath(group) + "/bills", new BillCreate(draft.Id, recipient.Character.Id, amount, draft.Service));
        }
        ImGui.EndDisabled();
        ImGui.EndDisabled();
    }

    private ContextBillDraft? ContextBillFor(CommandRequest request) =>
        contextBill is { } draft && request.Operation.Handler == CommandHandler.Billing && request.Json is { } json
            && JsonSerializer.Deserialize<BillCreate>(json, FoundationClient.Json)?.BillId == draft.Id ? draft : null;

    private async Task<Member?> FindSeasonCharacterAsync(Uri uri, string token, Guid season, CharacterTarget target, CancellationToken ct)
    {
        var path = $"api/v2/seasons/{season}/members?search=" + Uri.EscapeDataString(target.Name);
        var world = target.HomeWorldId.ToString(CultureInfo.InvariantCulture);
        string? cursor = null;
        Member? match = null;
        do
        {
            var page = await client.GetAsync<MemberPage>(uri, path + (cursor == null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), token, ct);
            foreach (var member in page.Items)
            {
                if (member.Status != "active" || member.Character.DisplayName != target.Name || member.Character.HomeWorldId != world) continue;
                if (match != null) return null;
                match = member;
            }
            cursor = page.NextCursor;
        } while (cursor != null);

        return match;
    }
}
