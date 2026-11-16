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
    // The shared header contains the selector and community entry buttons. Background work never adds
    // rows here; exceptional feedback lives below the content, not above it.
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
                ImGui.BeginDisabled(pending.Read().Operation.Kind == CommandKind.Unknown);
                if (ImGui.SmallButton("Retry original action")) ExecutePending(pending, false, recovering: true);
                ImGui.EndDisabled();
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
                configuration.PendingCommands.Remove(pending); save(); ApplyFormSuccess(pending.Read(), null);
                NoteAuthorizationReceipt(pending.Read(), receipt.Id);
                if (pending.Read().Operation.Handler == CommandHandler.SeasonForm) selected = receipt.CommunityId;
                actionResult = "Your action was saved. Any chat message has not been sent again.";
                status = "Refreshing current state."; refreshNeeded = true;
            };
        }, "Checking receipt…");
    }
}
