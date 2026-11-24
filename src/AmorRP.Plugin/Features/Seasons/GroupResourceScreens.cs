using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<GroupStorage> groupStorageRefresh = new(TimeSpan.FromSeconds(30));
    private readonly BackgroundRefresh<HoldingPage> groupDepositRefresh = new(TimeSpan.FromSeconds(30));
    private HoldingPage groupDeposits = new([], null, "0");
    private int depositLoadCount;
    private readonly BackgroundRefresh<GroupShifts> groupShiftsRefresh = new(TimeSpan.FromSeconds(30));
    private Guid? resourceGroup, stockSelection, depositSelection, shiftAssignee;
    private GroupStock[] groupStock = [];
    private GroupShift[] groupShifts = [];
    private int depositAmount = 1, withdrawAmount = 1;
    private string shiftStart = "", shiftEnd = "";

    private void SetResourceGroup(Guid? group)
    {
        if (resourceGroup == group) return;
        resourceGroup = group;
        groupStorageRefresh.Reset();
        groupDepositRefresh.Reset();
        groupDeposits = new([], null, "0");
        depositLoadCount = 1;
        groupShiftsRefresh.Reset();
        groupStock = [];
        groupShifts = [];
        depositSelection = stockSelection = shiftAssignee = null;
        depositAmount = withdrawAmount = 1;
        shiftStart = DateTimeOffset.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm");
        shiftEnd = DateTimeOffset.UtcNow.AddHours(2).ToString("yyyy-MM-dd HH:mm");
    }

    private void DrawGroupStorage(GroupProfile group)
    {
        SetResourceGroup(group.Id);
        var now = DateTimeOffset.UtcNow;
        if (groupStorageRefresh.TryCollect(now, dataGeneration, out var stock)) groupStock = stock.Items;
        var path = GroupPath(group) + "/storage";
        var token = session!.AccessToken;
        var uri = origin!;
        groupStorageRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<GroupStorage>(uri, path, token, ct));
        DrawRefreshState(groupStorageRefresh, "Group storage");
        if (ImGui.BeginTable("GroupStock", 3, ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();
            foreach (var item in groupStock)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Selectable(item.Name + "###stock" + item.Id, stockSelection == item.Id, ImGuiSelectableFlags.SpanAllColumns))
                { stockSelection = item.Id; withdrawAmount = 1; }
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(TypeName(item.TypeId));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(item.Quantity.ToString("N0"));
            }
            ImGui.EndTable();
        }
        if (groupStorageRefresh.HasLoaded && groupStock.Length == 0) ImGui.TextDisabled("Storage is empty.");
        if (CanGroupAction(group, GroupCapabilities.StorageWithdraw)
            && groupStock.FirstOrDefault(x => x.Id == stockSelection) is { } selected)
        {
            ImGui.InputInt("Amount##WithdrawStock", ref withdrawAmount);
            ImGui.BeginDisabled(withdrawAmount < 1 || withdrawAmount > selected.Quantity);
            if (ActionButton("Withdraw")) Mutate("POST", path, new GroupStockMove(selected.Id, selected.Version, withdrawAmount, true));
            ImGui.EndDisabled();
        }
        if (!CanGroupAction(group, GroupCapabilities.Storage)) return;
        ImGui.Separator();
        if (groupDepositRefresh.TryCollect(now, dataGeneration, out var page)) groupDeposits = page;
        var requested = Math.Max(depositLoadCount, groupDeposits.Items.Length);
        var inventory = $"api/v2/seasons/{group.CommunityId}/inventory?sort=name&typeIds="
            + string.Join(",", CraftingTypes.Consumables);
        groupDepositRefresh.Start(now, dataGeneration, requests.Token, ct => ReadGroupDepositsAsync(uri, token, inventory, requested, ct));
        DrawRefreshState(groupDepositRefresh, "Deposit items");
        var items = groupDeposits.Items.Where(x => x.DefinitionId != null && x.Available > 0).ToArray();
        var deposit = items.FirstOrDefault(x => x.Id == depositSelection);
        if (ImGui.BeginCombo("From your inventory", deposit?.Name ?? "Choose a stack"))
        {
            foreach (var item in items)
                if (ImGui.Selectable($"{item.Name} ×{item.Available}###deposit{item.Id}", depositSelection == item.Id))
                { depositSelection = item.Id; depositAmount = 1; }
            ImGui.EndCombo();
        }
        if (groupDeposits.NextCursor != null && ActionButton("More inventory items", mutation: false))
        {
            depositLoadCount = groupDeposits.Items.Length + 1;
            groupDepositRefresh.Invalidate();
        }
        if (deposit == null) return;
        ImGui.InputInt("Amount##DepositStock", ref depositAmount);
        ImGui.BeginDisabled(depositAmount < 1 || depositAmount > deposit.Available);
        if (ActionButton("Deposit")) Mutate("POST", path, new GroupStockMove(deposit.Id, deposit.Version, depositAmount, false));
        ImGui.EndDisabled();
    }

    private async Task<HoldingPage> ReadGroupDepositsAsync(Uri uri, string token, string path, int count, CancellationToken ct)
    {
        var page = await client.GetAsync<HoldingPage>(uri, path, token, ct);
        var items = page.Items.ToList();
        while (items.Count < count && page.NextCursor != null)
        {
            page = await client.GetAsync<HoldingPage>(uri, path + "&cursor=" + Uri.EscapeDataString(page.NextCursor), token, ct);
            items.AddRange(page.Items);
        }
        return page with { Items = items.ToArray() };
    }

    private void DrawGroupShifts(GroupProfile group)
    {
        SetResourceGroup(group.Id);
        var now = DateTimeOffset.UtcNow;
        if (groupShiftsRefresh.TryCollect(now, dataGeneration, out var shifts)) groupShifts = shifts.Items;
        var path = GroupPath(group) + "/shifts";
        var token = session!.AccessToken;
        var uri = origin!;
        groupShiftsRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<GroupShifts>(uri, path, token, ct));
        DrawRefreshState(groupShiftsRefresh, "Staff schedule");
        var owner = group.OwnerCharacterId == session.Character.Id;
        foreach (var shift in groupShifts.Where(x => x.End > now))
        {
            ImGui.PushID(shift.Id.ToString());
            ImGui.TextUnformatted($"{shift.CharacterName} · {shift.Start.LocalDateTime:g} to {shift.End.LocalDateTime:g}");
            if (owner)
            {
                ImGui.SameLine();
                if (ActionButton("Cancel shift")) Mutate("DELETE", path + "/" + shift.Id, null);
            }
            ImGui.PopID();
        }
        if (groupShiftsRefresh.HasLoaded && !groupShifts.Any(x => x.End > now)) ImGui.TextDisabled("No upcoming shifts.");
        if (!owner) return;
        LoadGroupPrivate(group);
        var staff = groupPrivate?.Members.Where(x => x.IsOwner || x.Capabilities?.Contains(GroupCapabilities.Billing) == true).ToArray() ?? [];
        var assignee = staff.FirstOrDefault(x => x.CharacterId == shiftAssignee);
        if (ImGui.BeginCombo("Staff member", assignee?.Name ?? "Choose a staff member"))
        {
            foreach (var member in staff)
                if (ImGui.Selectable(member.Name + "###shift" + member.CharacterId, member.CharacterId == shiftAssignee))
                    shiftAssignee = member.CharacterId;
            ImGui.EndCombo();
        }
        if (groupPrivate?.NextCursor != null && ActionButton("More staff", mutation: false)) LoadGroupPrivate(group, true, true);
        ImGui.InputText("Start (UTC, YYYY-MM-DD HH:MM)", ref shiftStart, 32);
        ImGui.InputText("End (UTC, YYYY-MM-DD HH:MM)", ref shiftEnd, 32);
        var hasStart = EventTime(shiftStart, out var start);
        var hasEnd = EventTime(shiftEnd, out var end);
        ImGui.BeginDisabled(assignee == null || !hasStart || !hasEnd || end <= start);
        if (ActionButton("Schedule shift") && assignee != null)
            Mutate("POST", path, new GroupShiftCreate(assignee.CharacterId, start, end));
        ImGui.EndDisabled();
    }
}
