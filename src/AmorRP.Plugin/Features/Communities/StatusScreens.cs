using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using AmorRP.Contracts.History;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;
namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel
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
    // The shared header contains the selector and community entry buttons. Background work never adds
    // rows here; exceptional feedback lives below the content, not above it.
    private void DrawCommunitySelection()
    {
        var style = ImGui.GetStyle();
        var joinWidth = ImGui.CalcTextSize("Join").X + style.FramePadding.X * 2;
        var createWidth = ImGui.CalcTextSize("Create community").X + style.FramePadding.X * 2;
        var leaveWidth = ImGui.CalcTextSize("Leave").X + style.FramePadding.X * 2;
        var stacked = ImGui.GetContentRegionAvail().X < joinWidth + createWidth + leaveWidth + ImGui.CalcTextSize("Choose a community").X + style.ItemSpacing.X * 5;
        DrawCommunitySelector("##Community", stacked ? -1 : Math.Max(1, ImGui.GetContentRegionAvail().X - joinWidth - createWidth - leaveWidth - style.ItemSpacing.X * 3));
        if (!stacked) ImGui.SameLine(); if (ImGui.Button("Join", new(joinWidth, 0))) OpenJoinCommunity?.Invoke();
        ImGui.SameLine(); if (ImGui.Button("Create community", new(createWidth, 0))) OpenCreateCommunity?.Invoke();
        ImGui.SameLine(); ImGui.BeginDisabled(community == null || Owner || TradeBlocksCommunitySwitch);
        if (ActionButton("Leave")) LeaveCommunity();
        ImGui.EndDisabled();
    }
    public void DrawLauncherCommunitySelector() => DrawCommunitySelector("##LauncherCommunity", 200);
    private void DrawCommunitySelector(string id, float width)
    {
        ImGui.SetNextItemWidth(width);
        var preview = session == null ? "Sign in first" : community?.Name ?? (communities.Length == 0 ? "No communities yet" : "Choose a community");
        ImGui.BeginDisabled(session == null || communities.Length == 0 || WriteBlocked || Busy || session.AccessExpiresAt <= DateTimeOffset.UtcNow);
        if (ImGui.BeginCombo(id, preview))
        {
            var incoming = tradeInbox.Pending(DateTimeOffset.UtcNow).Select(x => x.CommunityId).ToHashSet();
            foreach (var entry in communities)
            {
                var note = incoming.Contains(entry.Id) ? " • trade request" : announcementUnread.GetValueOrDefault(entry.Id) > 0 ? " • new announcements" : "";
                if (ImGui.Selectable(entry.Name + note + "###community" + entry.Id, selected == entry.Id) && community?.Id != entry.Id) SwitchCommunity(entry);
            }
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
    }
    private bool HasActionFeedback(PendingCommand? pending) => pending != null || feedback.Messages.Count > 0 || connectionError.Length > 0;
    private float ActionFeedbackHeight(PendingCommand? pending)
    {
        if (!HasActionFeedback(pending)) return 0;
        var rows = (feedback.Messages.Count + (pending != null ? 1 : 0) + (connectionError.Length > 0 ? 1 : 0)) * 2;
        return Math.Min(ImGui.GetFrameHeightWithSpacing() * rows + ImGui.GetStyle().WindowPadding.Y * 2,
            Math.Max(ImGui.GetFrameHeightWithSpacing() * 2.5f, ImGui.GetWindowSize().Y * .4f));
    }
    private void DrawActionFeedback(PendingCommand? pending)
    {
        if (!HasActionFeedback(pending)) return;
        var running = pending != null && (queuedCommand?.Key == pending.Key || Busy && currentCommand?.Key == pending.Key);
        var recovery = pending != null && !running;
        ImGui.BeginChild("ActionFeedback", new System.Numerics.Vector2(0, ActionFeedbackHeight(pending)), true);
        if (connectionError.Length > 0)
        {
            Wrapped(connectionError);
            if (ImGui.SmallButton("Retry connection")) Refresh();
            ImGui.SameLine();
            if (ImGui.SmallButton("Connection details")) ImGui.OpenPopup("ConnectionDetails");
            if (ImGui.BeginPopup("ConnectionDetails")) { Wrapped(connectionDiagnostic); ImGui.EndPopup(); }
            ImGui.Separator();
        }
        if (pending != null)
        {
            Wrapped(recovery ? "We could not confirm whether this action was saved. Check its result before making another change." : "Saving action…");
            if (recovery)
            {
                ImGui.BeginDisabled(Busy);
                if (ImGui.SmallButton("Retry original action")) ExecutePending(pending, false, recovering: true);
                ImGui.SameLine();
                if (ImGui.SmallButton("Check action result")) CheckReceipt(pending);
                ImGui.EndDisabled();
            }
            ImGui.Separator();
        }
        foreach (var message in feedback.Messages.ToArray())
        {
            ImGui.PushID(message.Id.ToString());
            // A single-line preview keeps the footer compact; the full text remains in details.
            var preview = message.Text.Replace('\n', ' ');
            var room = Math.Max(30, ImGui.GetContentRegionAvail().X);
            while (preview.Length > 1 && ImGui.CalcTextSize(preview + "…").X > room)
                preview = preview[..System.Globalization.StringInfo.ParseCombiningCharacters(preview)[^1]];
            ImGui.TextUnformatted(preview.Length < message.Text.Length ? preview + "…" : preview);
            if (ImGui.SmallButton("Action details")) { message.Pause(DateTimeOffset.UtcNow); ImGui.OpenPopup("Details"); }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup) || ImGui.IsItemActive()) message.Pause(DateTimeOffset.UtcNow);
            ImGui.SameLine();
            if (ImGui.SmallButton("Dismiss result")) feedback.Dismiss(message.Id);
            if (ImGui.BeginPopup("Details"))
            {
                message.Pause(DateTimeOffset.UtcNow);
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 420);
                ImGui.TextUnformatted(message.Text);
                if (message.Details.Length > 0) ImGui.TextUnformatted(message.Details);
                ImGui.PopTextWrapPos(); ImGui.EndPopup();
            }
            ImGui.PopID();
        }
        ImGui.EndChild();
    }
    private void CheckReceipt(PendingCommand pending)
    {
        var uri = origin!; var token = session!.AccessToken;
        Start(async ct => {
            var receipt = await client.GetAsync<Operation>(uri, $"api/v2/operation-keys/{pending.Key}", token, ct);
            return () => {
                configuration.PendingCommands.Remove(pending); save(); ApplyFormSuccess(pending.Read().CurrentShape(), null);
                NoteAuthorizationReceipt(pending.Read().CurrentShape(), receipt.Id);
                if (pending.Read().Operation.Handler == CommandHandler.CommunityForm) selected = receipt.CommunityId;
                actionResult = "Your action was saved. Any chat message has not been sent again.";
                status = "Refreshing current state."; refreshNeeded = true;
            };
        }, "Checking receipt…");
    }
}
