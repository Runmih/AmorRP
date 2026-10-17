using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AmorRP.Server.IntegrationTests;

public sealed class BootstrapTests : IAsyncLifetime
{
    private string adminConnection = null!;
    private string connection = null!;
    private string databaseName = null!;

    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AMORRP_TEST_DATABASE")
            ?? throw new InvalidOperationException("Set AMORRP_TEST_DATABASE to a disposable PostgreSQL test database (name must start with amorrp_test).");
        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (builder.Database?.StartsWith("amorrp_test", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Integration tests require a database name starting with amorrp_test.");
        adminConnection = builder.ConnectionString;
        databaseName = "amorrp_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await command.ExecuteNonQueryAsync();
        builder.Database = databaseName;
        connection = builder.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is null)
            return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Unmigrated_database_is_unready_then_migration_makes_it_ready()
    {
        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        await AssertUnavailable(client);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AmorDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        var ready = await WaitForReadiness(client, HttpStatusCode.OK);
        Assert.Equal("ok", ready?.Status);

        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync(TestContext.Current.CancellationToken);
        await using var corrupt = new NpgsqlCommand("UPDATE infrastructure_state SET schema_version = 999", db);
        await corrupt.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await AssertUnavailable(client);
    }

    [Fact]
    public async Task Database_outage_keeps_liveness_and_returns_redacted_readiness()
    {
        var invalid = new NpgsqlConnectionStringBuilder(connection) { Port = 1, Timeout = 1 };
        using var factory = CreateFactory(invalid.ConnectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode);
        await AssertUnavailable(client);
    }

    [Fact]
    public async Task Capabilities_match_wire_contract_without_advertising_unbuilt_features()
    {
        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<CapabilitiesResponse>("/api/v2/capabilities", TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal("2", result.ApiVersion);
        Assert.Equal(new[] { "potion", "letter", "book", "note" }, result.SupportedTypes.Select(x => x.Id));
        Assert.Equal(new[] { "currency.manage", "items.potion.create", "inventory.remove", "items.potion.define", "community.moderate", "announcements.manage", "members.gameplay.manage", "groups.create", "groups.manage" }, result.GrantableCapabilities);
        Assert.Equal(new[] { "emote", "say", "party", "linkshell", "crossworld-linkshell" }, result.ChatDestinationKinds);
        Assert.Equal(3, result.Limits.OwnedCommunities);
        Assert.Equal(6, result.Limits.JoinedCommunities);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v2/communities", null, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public void Inconsistent_community_policy_prevents_server_startup()
    {
        using var factory = CreateFactory(connection, "2");
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    private static async Task AssertUnavailable(HttpClient client)
    {
        var response = await WaitForReadiness(client, HttpStatusCode.ServiceUnavailable);
        Assert.Equal("unavailable", response.Status);
    }

    private static async Task<HealthResponse> WaitForReadiness(HttpClient client, HttpStatusCode status)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            using var response = await client.GetAsync("/health/ready", timeout.Token);
            if (response.StatusCode == status)
            {
                var json = await response.Content.ReadAsStringAsync(timeout.Token);
                Assert.Equal(status == HttpStatusCode.OK ? "{\"status\":\"ok\"}" : "{\"status\":\"unavailable\"}", json);
                Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
                return (await response.Content.ReadFromJsonAsync<HealthResponse>(timeout.Token))!;
            }
            await Task.Delay(25, timeout.Token);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, string joinedCommunities = "6") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Database", connectionString);
            builder.UseSetting("ServicePolicy:JoinedCommunities", joinedCommunities);
            builder.UseSetting("HealthPolicy:ReadinessRefreshSeconds", "1");
        });
}
