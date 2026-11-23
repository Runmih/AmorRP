namespace AmorRP.Plugin.Services.Ui;

internal sealed class FeedbackQueue
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(20);
    private readonly List<Message> messages = [];
    private readonly List<Message> history = [];
    private long nextId;
    public IReadOnlyList<Message> History => history;
    public void ClearHistory() => history.Clear();
    public IReadOnlyList<Message> Messages => messages;
    public Message? Latest(string origin) => messages.LastOrDefault(x => x.Origin == origin);
    public void Add(string origin, string text, DateTimeOffset now)
    {
        if (text.Length == 0) { Clear(origin); return; }
        var last = messages.LastOrDefault();
        if (last?.Origin == origin && last.Text == text) { last.Remaining = Lifetime; last.UpdatedAt = now; return; }
        if (messages.Count == 3) messages.RemoveAt(0);
        messages.Add(AddHistory(origin, text, now));
    }

    public Message AddHistory(string origin, string text, DateTimeOffset now, string details = "")
    {
        var message = new Message(++nextId, origin, text, now) { Details = details };
        if (history.Count == 100) history.RemoveAt(0);
        history.Add(message);
        return message;
    }

    public void Clear(string? origin = null) => messages.RemoveAll(x => origin == null || x.Origin == origin);
    public void Dismiss(long id) => messages.RemoveAll(x => x.Id == id);
    public void Tick(DateTimeOffset now, bool protectAction)
    {
        foreach (var message in messages)
        {
            var countingFrom = message.PauseUntil > message.UpdatedAt ? message.PauseUntil : message.UpdatedAt;
            if (now > countingFrom && !(protectAction && message.Origin == "action")) message.Remaining -= now - countingFrom;
            message.UpdatedAt = now;
        }
        messages.RemoveAll(x => x.Remaining <= TimeSpan.Zero && !(protectAction && x.Origin == "action"));
    }
    public sealed class Message(long id, string origin, string text, DateTimeOffset now)
    {
        public long Id { get; } = id;
        public DateTimeOffset CreatedAt { get; } = now;
        public string Origin { get; } = origin;
        public string Text { get; set; } = text;
        public string Details { get; set; } = "";
        public TimeSpan Remaining { get; set; } = Lifetime;
        public DateTimeOffset UpdatedAt { get; set; } = now;
        public DateTimeOffset PauseUntil { get; private set; }
        // Cover independent framework/UI tick rates, and resume shortly after the last hover.
        public void Pause(DateTimeOffset now) => PauseUntil = now.AddMilliseconds(250);
    }
}
