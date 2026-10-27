using System.Net;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class RequestCoordinatorTests
{
    private static TaskCompletionSource<Action> Reply() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static FoundationApiException Unauthorized() => new(HttpStatusCode.Unauthorized, "authentication_required", "Old token");

    [Fact]
    public async Task Character_switch_cancels_all_lanes_and_late_results_cannot_replace_the_new_character()
    {
        using var requests = new RequestCoordinator();
        var oldForeground = Reply(); var oldSync = Reply(); var entered = new TaskCompletionSource(Creation);
        var syncEntered = new TaskCompletionSource(Creation);
        var shown = "old character"; var oldToken = requests.Token;
        var foreground = requests.StartForeground(_ => { entered.SetResult(); return oldForeground.Task; })!;
        var sync = requests.StartSynchronization(_ => { syncEntered.SetResult(); return oldSync.Task; });
        await entered.Task; await syncEntered.Task;
        requests.Reset(); shown = "new character";
        oldForeground.SetResult(() => shown = "stale login"); oldSync.SetException(Unauthorized());
        await foreground;
        try { await sync; } catch (Exception error) when (error is FoundationApiException or OperationCanceledException) { }
        Assert.True(oldToken.IsCancellationRequested);
        Assert.False(requests.TakeForeground(out _, out _));
        Assert.False(requests.TakeSynchronization(false, out _, out _));
        Assert.Equal("new character", shown);
        await requests.StartForeground(_ => Task.FromResult<Action>(() => shown = "new login"))!;
        Assert.True(requests.TakeForeground(out var current, out var stale));
        Assert.False(stale); (await current)(); Assert.Equal("new login", shown);
    }

    private const TaskCreationOptions Creation = TaskCreationOptions.RunContinuationsAsynchronously;

    [Fact]
    public async Task Community_switch_discards_both_a_late_foreground_failure_and_a_successful_core_read()
    {
        using var requests = new RequestCoordinator();
        var failed = Reply(); var core = Reply();
        var foreground = requests.StartForeground(_ => failed.Task)!;
        var sync = requests.StartSynchronization(_ => core.Task);
        requests.ChangeView();
        failed.SetException(new HttpRequestException("previous community")); core.SetResult(() => throw new Exception("Must not apply"));
        await Assert.ThrowsAsync<HttpRequestException>(async () => await foreground);
        await sync;
        Assert.True(requests.TakeForeground(out _, out var staleForeground)); Assert.True(staleForeground);
        Assert.True(requests.TakeSynchronization(false, out _, out var staleSync)); Assert.True(staleSync);
        Assert.False(requests.ForegroundBusy); Assert.False(requests.SynchronizationBusy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_old_core_response_waits_for_renewal_and_is_discarded_after_new_tokens_are_accepted(bool unauthorized)
    {
        using var requests = new RequestCoordinator();
        var old = Reply(); var renewed = Reply();
        var signedIn = true;
        var core = requests.StartSynchronization(_ => old.Task);
        var renewal = requests.StartForeground(_ => renewed.Task)!;
        if (unauthorized) old.SetException(Unauthorized());
        else old.SetResult(() => throw new Exception("Old-token result must not apply"));
        try { await core; } catch (FoundationApiException) { }
        Assert.False(requests.TakeSynchronization(true, out _, out _));
        renewed.SetResult(() => requests.ChangeData()); await renewal;
        Assert.True(requests.TakeForeground(out var accepted, out var staleRenewal));
        Assert.False(staleRenewal); (await accepted)();
        Assert.True(requests.TakeSynchronization(false, out var response, out var staleRead));
        if (!staleRead && response.IsFaulted) signedIn = false;
        Assert.True(staleRead); Assert.True(signedIn);
    }

    [Fact]
    public async Task Failed_renewal_does_not_hide_a_current_token_authentication_failure()
    {
        using var requests = new RequestCoordinator();
        var core = requests.StartSynchronization(_ => Task.FromException<Action>(Unauthorized()));
        var renewal = requests.StartForeground(_ => Task.FromException<Action>(new HttpRequestException("offline")))!;
        await Assert.ThrowsAsync<FoundationApiException>(async () => await core);
        await Assert.ThrowsAsync<HttpRequestException>(async () => await renewal);
        Assert.False(requests.TakeSynchronization(true, out _, out _));
        Assert.True(requests.TakeForeground(out var failedRenewal, out var staleRenewal));
        Assert.False(staleRenewal); Assert.True(failedRenewal.IsFaulted);
        Assert.True(requests.TakeSynchronization(false, out var failedRead, out var staleRead));
        Assert.False(staleRead);
        Assert.IsType<FoundationApiException>(failedRead.Exception!.GetBaseException());
    }

    [Fact]
    public async Task A_slow_core_read_does_not_block_foreground_work_and_overlapping_starts_are_single_flight()
    {
        using var requests = new RequestCoordinator(); var blocked = Reply(); var foregroundReply = Reply();
        var calls = 0;
        var sync = requests.StartSynchronization(_ => { Interlocked.Increment(ref calls); return blocked.Task; });
        Assert.Same(sync, requests.StartSynchronization(_ => throw new Exception("Duplicate core request")));
        var foreground = requests.StartForeground(_ => foregroundReply.Task)!;
        Assert.Null(requests.StartForeground(_ => throw new Exception("Duplicate foreground request")));
        var applied = false; foregroundReply.SetResult(() => applied = true); await foreground;
        Assert.True(requests.TakeForeground(out var result, out var stale)); Assert.False(stale); (await result)();
        Assert.True(applied); Assert.True(requests.SynchronizationBusy);
        blocked.SetResult(() => { }); await sync;
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task A_write_invalidates_core_reads_but_its_own_acknowledgment_is_still_applied()
    {
        using var requests = new RequestCoordinator(); var read = Reply(); var write = Reply();
        var core = requests.StartSynchronization(_ => read.Task);
        var command = requests.StartForeground(_ => write.Task)!;
        requests.ChangeData();
        var acknowledged = false; write.SetResult(() => acknowledged = true); read.SetResult(() => { });
        await command; await core;
        Assert.True(requests.TakeForeground(out var receipt, out var staleCommand)); Assert.False(staleCommand);
        (await receipt)(); Assert.True(acknowledged);
        Assert.True(requests.TakeSynchronization(false, out _, out var staleCore)); Assert.True(staleCore);
    }

    [Fact]
    public async Task Context_cancellation_also_discards_independent_feature_results()
    {
        using var requests = new RequestCoordinator();
        using var inventory = new BackgroundRefresh<string>(TimeSpan.FromMinutes(1));
        var reply = new TaskCompletionSource<string>(Creation); var now = DateTimeOffset.UtcNow;
        inventory.Start(now, requests.DataGeneration, requests.Token, _ => reply.Task);
        requests.Reset(); reply.SetResult("previous character inventory"); await reply.Task;
        Assert.False(inventory.TryCollect(now, requests.DataGeneration, out _));
        Assert.Null(inventory.Error);
        inventory.Start(now, requests.DataGeneration, requests.Token, _ => Task.FromResult("new inventory"));
        Assert.True(inventory.TryCollect(now, requests.DataGeneration, out var value)); Assert.Equal("new inventory", value);
    }

    [Fact]
    public void Recovered_commands_from_another_community_cannot_update_the_current_editor()
    {
        var oldCommunity = Guid.NewGuid(); var active = Guid.NewGuid();
        var profile = new CommandRequest("PATCH", $"api/v2/communities/{oldCommunity}", "{}", "etag");
        var member = new CommandRequest("PATCH", $"api/v2/communities/{oldCommunity}/members/{Guid.NewGuid()}/description", "{}", "etag");
        Assert.False(profile.AppliesToCommunity(active)); Assert.False(member.AppliesToCommunity(active));
        Assert.False(profile.AppliesToCommunity(null)); Assert.True(profile.AppliesToCommunity(oldCommunity));
        Assert.True(new CommandRequest("POST", "api/v2/communities", "{}", null).AppliesToCommunity(active));
    }
}
