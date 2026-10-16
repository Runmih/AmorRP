namespace AmorRP.Plugin.Features.Groups;

public sealed partial class GroupsPanel
{
    private static readonly TimeSpan FeedbackLifetime = Services.Ui.FeedbackQueue.Lifetime;
    private readonly Services.Ui.FeedbackQueue feedback = new();
    private DateTimeOffset tradeOutcomeUntil;
    private string actionResult
    {
        get => feedback.Latest("action")?.Text ?? "";
        set => feedback.Add("action", value, DateTimeOffset.UtcNow);
    }
    private string actionDiagnostic
    {
        get => feedback.Latest("action")?.Details ?? "";
        set { if (feedback.Latest("action") is { } message) message.Details = value; }
    }
    private string tradeFeedback
    {
        get => feedback.Latest("trade")?.Text ?? "";
        set => feedback.Add("trade", value, DateTimeOffset.UtcNow);
    }
    private bool HasUnresolvedAction => configuration.PendingCommands.Any(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session?.Character.Id);

    public void WindowOpened() => ClearTransientFeedback();

    private void ClearTransientFeedback()
    {
        // An uncertain command keeps its durable retry key and receipt controls.
        if (!HasUnresolvedAction) { actionResult = ""; actionDiagnostic = ""; announcementPostError = ""; }
        tradeFeedback = "";
        if (SelectedTrade is { } trade && !Active(trade)) tradeSelection = null;
    }

    private void ExpireFeedback()
    {
        var now = DateTimeOffset.UtcNow;
        feedback.Tick(now, HasUnresolvedAction);
        if (SelectedTrade is { } trade && !Active(trade) && now >= tradeOutcomeUntil) tradeSelection = null;
    }
}
