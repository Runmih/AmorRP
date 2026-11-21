using AmorRP.Contracts.Currency;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<GroupBillingSettings> groupBillingRefresh = new(TimeSpan.FromSeconds(20));
    private GroupBillingSettings? groupBillingSettings;
    private string groupBillingEtag = "";
    private int commissionDraft, commissionSaved;
    private bool requireClockInDraft, requireClockInSaved;
    private bool GroupBillingDirty => commissionDraft != commissionSaved || requireClockInDraft != requireClockInSaved;

    private void ClearGroupBillingSettings()
    {
        groupBillingRefresh.Reset();
        groupBillingSettings = null;
        groupBillingEtag = "";
    }

    private void AcceptGroupBillingSettings(GroupBillingSettings settings)
    {
        groupBillingSettings = settings;
        if (groupBillingEtag.Length > 0 && GroupBillingDirty) return;
        groupBillingEtag = settings.Etag;
        commissionDraft = commissionSaved = settings.CommissionPercent;
        requireClockInDraft = requireClockInSaved = settings.RequireClockIn;
    }

    private bool DrawGroupBillingSettings(GroupProfile group)
    {
        var now = DateTimeOffset.UtcNow;
        if (groupBillingRefresh.TryCollect(now, dataGeneration, out var loaded)) AcceptGroupBillingSettings(loaded);
        if (!SessionRenewalInProgress && !WriteBlocked && origin is { } uri && session is { } actor)
        {
            var path = GroupPath(group) + "/billing-settings";
            var token = actor.AccessToken;
            groupBillingRefresh.Start(now, dataGeneration, requests.Token,
                ct => client.GetAsync<GroupBillingSettings>(uri, path, token, ct));
        }

        DrawRefreshState(groupBillingRefresh, "Billing settings");
        if (groupBillingSettings is not { } settings) return false;
        Wrapped($"Cashier commission: {settings.CommissionPercent}% after tax");
        var clockedIn = settings.ClockedInAt.HasValue;
        if (clockedIn) Wrapped($"Clocked in since {settings.ClockedInAt!.Value.LocalDateTime:g}");
        if (ActionButton(clockedIn ? "Clock out" : "Clock in"))
            Mutate("PUT", GroupPath(group) + "/clock-in", new ClockInEdit(!clockedIn));

        if (OwnSelectedGroup && ImGui.CollapsingHeader("Group billing settings"))
        {
            FieldLabel("Cashier commission after tax (%)");
            ImGui.InputInt("##CashierCommission", ref commissionDraft);
            ImGui.Checkbox("Require clock-in to issue bills", ref requireClockInDraft);
            var stale = groupBillingEtag != settings.Etag;
            if (stale)
            {
                Wrapped("Billing settings changed. Reload before saving.");
                if (ImGui.Button("Reload group billing settings"))
                {
                    groupBillingEtag = "";
                    AcceptGroupBillingSettings(settings);
                }
            }
            ImGui.BeginDisabled(stale || !GroupBillingDirty || commissionDraft is < 0 or > 100);
            if (ActionButton("Save group billing settings"))
                Mutate("PUT", GroupPath(group) + "/billing-settings", new GroupBillingEdit(commissionDraft, requireClockInDraft), groupBillingEtag);
            ImGui.EndDisabled();
        }
        var mayBill = clockedIn || !settings.RequireClockIn;
        if (!mayBill) Wrapped("Clock in to charge customers.");
        return mayBill;
    }

    private void ApplyGroupBillingSuccess(CommandRequest request, string json)
    {
        if (request.Operation.Kind == CommandKind.EditGroupBilling) groupBillingEtag = "";
        AcceptGroupBillingSettings(ReadCommand<GroupBillingSettings>(json));
        groupBillingRefresh.Invalidate();
        groupPrivate = null;
        privateRequestedFor = null;
    }
}
