using AmorRP.Plugin.Services.Ui;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class FeedbackQueueTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-16T12:00:00Z");
    [Fact]
    public void Keeps_three_recent_results_and_each_results_own_details()
    {
        var queue = new FeedbackQueue();
        queue.Add("action", "First", Start); queue.Latest("action")!.Details = "Receipt one";
        queue.Add("trade", "Second", Start); queue.Add("action", "Third", Start);
        Assert.Equal("Receipt one", queue.Messages[0].Details);
        queue.Add("trade", "Fourth", Start);
        Assert.Equal(new[] { "Second", "Third", "Fourth" }, queue.Messages.Select(x => x.Text));
        queue.Dismiss(queue.Messages[1].Id);
        Assert.Equal(new[] { "Second", "Fourth" }, queue.Messages.Select(x => x.Text));
    }
    [Fact]
    public void Hover_and_open_details_pause_expiry_and_countdown_resumes_afterward()
    {
        var queue = new FeedbackQueue(); queue.Add("action", "Read me", Start);
        queue.Tick(Start.AddSeconds(19), false);
        for (var tick = 190; tick < 500; tick++)
        {
            queue.Messages[0].Pause(Start.AddMilliseconds(tick * 100));
            queue.Tick(Start.AddMilliseconds(tick * 100 + 50), false);
        }
        Assert.Single(queue.Messages);
        queue.Tick(Start.AddSeconds(50.5), false); Assert.Single(queue.Messages);
        queue.Tick(Start.AddSeconds(51.5), false); Assert.Empty(queue.Messages);
    }
    [Fact]
    public void Ordinary_results_expire_after_twenty_seconds_but_recovery_is_protected()
    {
        var queue = new FeedbackQueue(); queue.Add("action", "Uncertain", Start); queue.Add("trade", "Updated", Start);
        queue.Tick(Start.AddSeconds(20), true); Assert.Single(queue.Messages);
        queue.Clear("trade"); Assert.Equal("Uncertain", queue.Messages[0].Text);
        queue.Tick(Start.AddSeconds(39), false); Assert.Single(queue.Messages);
        queue.Tick(Start.AddSeconds(40), false); Assert.Empty(queue.Messages);
    }
}
