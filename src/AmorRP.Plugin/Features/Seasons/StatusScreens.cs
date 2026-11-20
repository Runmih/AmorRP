using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using AmorRP.Contracts.History;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private void DrawLoginFeedback()
    {
        if (loginFeedback.Length == 0) return;
        Wrapped(loginFeedback);
        if (loginDiagnostic.Length == 0) return;
        if (ImGui.SmallButton("Login details")) ImGui.OpenPopup("LoginDetails");
        if (ImGui.BeginPopup("LoginDetails")) { Wrapped(loginDiagnostic); ImGui.EndPopup(); }
    }
    private string connectionError = "", connectionDiagnostic = "";
    // Keep the selector stable while background work reports to the Activity Log.
    private void DrawCommunitySelection()
    {
        if (CanCreateSeasons)
        {
            var width = ImGui.CalcTextSize("Create season").X + ImGui.GetStyle().FramePadding.X * 2;
            DrawCommunitySelector("##Season", Math.Max(1, ImGui.GetContentRegionAvail().X - width - ImGui.GetStyle().ItemSpacing.X));
            ImGui.SameLine();
            if (ImGui.Button("Create season", new(width, 0))) OpenCreateCommunity?.Invoke();
        }
        else DrawCommunitySelector("##Season", -1);
        DrawSeasonState();
    }
    private void DrawCommunitySelector(string id, float width)
    {
        ImGui.SetNextItemWidth(width);
        var preview = session == null ? "Sign in first" : community?.Name ?? (communities.Length == 0 ? $"No season yet" : $"Choose a season");
        ImGui.BeginDisabled(session == null || communities.Length == 0 || WriteBlocked || Busy || session.AccessExpiresAt <= DateTimeOffset.UtcNow);
        if (ImGui.BeginCombo(id, preview))
        {
            var incoming = tradeInbox.Pending(DateTimeOffset.UtcNow).Select(x => x.CommunityId).ToHashSet();
            foreach (var entry in communities)
            {
                var note = incoming.Contains(entry.Id) ? " • trade request" : announcementUnread.GetValueOrDefault(entry.Id) > 0 ? " • new announcements" : "";
                var state = " · " + entry.SeasonState;
                if (ImGui.Selectable(entry.Name + state + note + "###community" + entry.Id, selected == entry.Id) && community?.Id != entry.Id) SwitchCommunity(entry);
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
    }
    private static void DrawSectionHeading(string title)
    {
        ImGui.TextColored(InventoryGold, title);
        ImGui.Separator();
    }

    public void DrawActivityLog()
    {
        var pending = session == null ? null : configuration.PendingCommands.FirstOrDefault(
            x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session.Character.Id);
        if (pending != null)
        {
            var running = queuedCommand?.Key == pending.Key || Busy && currentCommand?.Key == pending.Key;
            DrawSectionHeading("Pending action");
            Wrapped(running ? "Saving action…" : "Check this action's result before making another change.");
            Wrapped("Action ID: " + pending.Key);
            if (!running)
            {
                ImGui.BeginDisabled(Busy);
                ImGui.BeginDisabled(pending.Read().Operation.Kind == CommandKind.Unknown);
                if (ImGui.Button("Retry original action")) ExecutePending(pending, false, recovering: true);
                ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Check action result")) CheckReceipt(pending);
                ImGui.EndDisabled();
            }
            ImGui.Spacing();
        }
        if (connectionError.Length > 0)
        {
            Wrapped(connectionError);
            Wrapped(connectionDiagnostic);
            ImGui.BeginDisabled(session == null || Busy || SyncBusy);
            if (ImGui.Button("Retry connection")) Refresh();
            ImGui.EndDisabled();
        }
        if (loginFeedback.Length > 0)
        {
            Wrapped(loginFeedback);
            if (loginDiagnostic.Length > 0) Wrapped(loginDiagnostic);
        }
        if (feedback.History.Count == 0)
        {
            ImGui.TextDisabled("No recent activity.");
            return;
        }
        DrawSectionHeading("Recent activity");
        foreach (var message in feedback.History.Reverse().ToArray())
        {
            ImGui.PushID(message.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ImGui.TextDisabled($"{message.CreatedAt.ToLocalTime():HH:mm:ss} · {message.Origin}");
            Wrapped(message.Text);
            if (message.Details.Length > 0 && ImGui.TreeNode("Details"))
            {
                Wrapped(message.Details);
                ImGui.TreePop();
            }
            ImGui.Separator();
            ImGui.PopID();
        }
    }

    private void CheckReceipt(PendingCommand pending)
    {
        var uri = origin!; var token = session!.AccessToken;
        Start(async ct => {
            var receipt = await client.GetAsync<Operation>(uri, $"api/v2/operation-keys/{pending.Key}", token, ct);
            return () => {
                configuration.PendingCommands.Remove(pending); save(); ApplyFormSuccess(pending.Read(), null);
                NoteAuthorizationReceipt(pending.Read(), receipt.Id);
                if (pending.Read().Operation.Handler == CommandHandler.SeasonForm) selected = receipt.CommunityId;
                actionResult = "Your action was saved. Any chat message has not been sent again.";
                status = "Refreshing current state."; refreshNeeded = true;
            };
        }, "Checking receipt…");
    }
}
