using System.Net;
using System.Text;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class BackgroundRefreshTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T12:00:00Z");

    [Fact]
    public async Task A_failed_events_request_does_not_prevent_accepting_inventory_from_the_same_server()
    {
        using var client = new FoundationClient(new FeatureReplies(), TimeSpan.FromSeconds(5));
        using var events = new BackgroundRefresh<FavoriteEvents>(TimeSpan.FromSeconds(30));
        using var inventory = new BackgroundRefresh<HoldingPage>(TimeSpan.FromMinutes(1));
        var uri = new Uri("https://server.example/");
        var eventsTask = client.GetAsync<FavoriteEvents>(uri, "api/v2/me/favorite-group-events", "test-token", TestContext.Current.CancellationToken);
        var inventoryTask = client.GetAsync<HoldingPage>(uri, "api/v2/communities/c/inventory", "test-token", TestContext.Current.CancellationToken);
        events.Start(Now, 1, CancellationToken.None, _ => eventsTask);
        inventory.Start(Now, 1, CancellationToken.None, _ => inventoryTask);

        await Assert.ThrowsAsync<FoundationApiException>(() => eventsTask);
        await inventoryTask;

        Assert.False(events.TryCollect(Now, 1, out _));
        Assert.IsType<FoundationApiException>(events.Error);
        Assert.True(inventory.TryCollect(Now, 1, out var loaded));
        Assert.Empty(loaded.Items);
        Assert.Equal("7", loaded.SnapshotVersion);
        Assert.True(inventory.HasLoaded);
        Assert.Null(inventory.Error);
    }

    [Fact]
    public void A_slow_events_request_does_not_delay_other_feature_results()
    {
        using var events = new BackgroundRefresh<string>(TimeSpan.FromSeconds(30));
        using var inventory = new BackgroundRefresh<string>(TimeSpan.FromMinutes(1));
        var delayed = new TaskCompletionSource<string>();
        events.Start(Now, 1, CancellationToken.None, _ => delayed.Task);
        inventory.Start(Now, 1, CancellationToken.None, _ => Task.FromResult("fresh inventory"));

        Assert.True(inventory.TryCollect(Now, 1, out var loaded));
        Assert.Equal("fresh inventory", loaded);
        Assert.False(events.TryCollect(Now, 1, out _));
        Assert.True(events.IsLoading);
    }

    [Fact]
    public void Failure_preserves_the_last_accepted_value_and_retries_without_a_manual_refresh()
    {
        using var refresh = new BackgroundRefresh<string>(TimeSpan.FromSeconds(30));
        refresh.Start(Now, 1, CancellationToken.None, _ => Task.FromResult("last confirmed events"));
        Assert.True(refresh.TryCollect(Now, 1, out var shown));
        refresh.Invalidate();
        refresh.Start(Now, 1, CancellationToken.None, _ => Task.FromException<string>(new HttpRequestException("offline")));
        if (refresh.TryCollect(Now, 1, out var failedValue)) shown = failedValue;
        Assert.Equal("last confirmed events", shown);
        Assert.True(refresh.HasLoaded);
        Assert.NotNull(refresh.Error);

        var requests = 0;
        refresh.Start(Now.AddSeconds(14), 1, CancellationToken.None, _ => { requests++; return Task.FromResult("too early"); });
        Assert.Equal(0, requests);
        refresh.Start(Now.AddSeconds(15), 1, CancellationToken.None, _ => Task.FromResult("updated events"));
        Assert.True(refresh.TryCollect(Now.AddSeconds(15), 1, out shown));
        Assert.Equal("updated events", shown);
        Assert.Null(refresh.Error);
    }

    [Fact]
    public void Mutation_or_token_rotation_discards_an_old_result_and_allows_a_fresh_read()
    {
        using var refresh = new BackgroundRefresh<string>(TimeSpan.FromMinutes(1));
        var old = new TaskCompletionSource<string>();
        CancellationToken oldToken = default;
        refresh.Start(Now, 1, CancellationToken.None, token => { oldToken = token; return old.Task; });

        Assert.False(refresh.TryCollect(Now, 2, out _));
        Assert.True(oldToken.IsCancellationRequested);
        refresh.Start(Now, 2, CancellationToken.None, _ => Task.FromResult("new token result"));
        Assert.True(refresh.TryCollect(Now, 2, out var current));
        Assert.Equal("new token result", current);
        old.SetResult("stale result");
        Assert.False(refresh.TryCollect(Now, 2, out _));
    }

    [Fact]
    public void An_old_authentication_error_is_not_applied_after_token_rotation()
    {
        using var refresh = new BackgroundRefresh<string>(TimeSpan.FromMinutes(1));
        refresh.Start(Now, 1, CancellationToken.None, _ => Task.FromException<string>(
            new FoundationApiException(HttpStatusCode.Unauthorized, "authentication_required", "Expired token")));
        Assert.False(refresh.TryCollect(Now, 1, out _));
        Assert.IsType<FoundationApiException>(refresh.Error);

        Assert.False(refresh.TryCollect(Now, 2, out _));
        Assert.Null(refresh.Error);
        refresh.Start(Now, 2, CancellationToken.None, _ => Task.FromResult("renewed"));
        Assert.True(refresh.TryCollect(Now, 2, out var value));
        Assert.Equal("renewed", value);
    }

    [Fact]
    public void Reset_cancels_a_previous_characters_request_and_ignores_its_late_completion()
    {
        using var refresh = new BackgroundRefresh<string>(TimeSpan.FromMinutes(1));
        var old = new TaskCompletionSource<string>();
        CancellationToken oldToken = default;
        refresh.Start(Now, 1, CancellationToken.None, token => { oldToken = token; return old.Task; });
        refresh.Reset();
        old.SetException(new HttpRequestException("previous character response"));

        Assert.True(oldToken.IsCancellationRequested);
        Assert.False(refresh.TryCollect(Now, 1, out _));
        Assert.Null(refresh.Error);
        Assert.False(refresh.HasLoaded);
        refresh.Start(Now, 1, CancellationToken.None, _ => Task.FromResult("new character"));
        Assert.True(refresh.TryCollect(Now, 1, out var current));
        Assert.Equal("new character", current);
    }

    [Fact]
    public void Repeated_ticks_do_not_duplicate_in_flight_requests_or_skip_the_refresh_interval()
    {
        using var refresh = new BackgroundRefresh<string>(TimeSpan.FromSeconds(30));
        var task = new TaskCompletionSource<string>();
        var requests = 0;
        Task<string> Load(CancellationToken _) { requests++; return task.Task; }
        refresh.Start(Now, 1, CancellationToken.None, Load);
        refresh.Start(Now.AddSeconds(1), 1, CancellationToken.None, Load);
        Assert.Equal(1, requests);
        task.SetResult("ready");
        Assert.True(refresh.TryCollect(Now.AddSeconds(2), 1, out _));
        refresh.Start(Now.AddSeconds(31), 1, CancellationToken.None, Load);
        Assert.Equal(1, requests);
        refresh.Start(Now.AddSeconds(32), 1, CancellationToken.None, Load);
        Assert.Equal(2, requests);
    }

    [Fact]
    public void Synchronous_loader_failures_are_contained_and_cancelled_reads_are_not_reported_as_errors()
    {
        using var refresh = new BackgroundRefresh<string>(TimeSpan.FromMinutes(1));
        refresh.Start(Now, 1, CancellationToken.None, _ => throw new HttpRequestException("cannot start"));
        Assert.False(refresh.TryCollect(Now, 1, out _));
        Assert.IsType<HttpRequestException>(refresh.Error);
        refresh.Reset();
        using var lifetime = new CancellationTokenSource();
        var pending = new TaskCompletionSource<string>();
        refresh.Start(Now, 1, lifetime.Token, _ => pending.Task);
        lifetime.Cancel();
        pending.SetResult("cancelled context");
        Assert.False(refresh.TryCollect(Now, 1, out _));
        Assert.Null(refresh.Error);
    }

    private sealed class FeatureReplies : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var failed = request.RequestUri!.AbsolutePath == "/api/v2/me/favorite-group-events";
            var json = failed ? """{"code":"service_unavailable","title":"Events unavailable"}"""
                : """{"items":[],"nextCursor":null,"snapshotVersion":"7"}""";
            return Task.FromResult(new HttpResponseMessage(failed ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
