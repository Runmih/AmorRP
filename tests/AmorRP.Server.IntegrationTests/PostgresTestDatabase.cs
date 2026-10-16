using Npgsql;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public abstract class PostgresTestDatabase : IAsyncLifetime
{
    protected string Connection = "";
    private string adminConnection = "";
    private string database = "";
    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AMORRP_TEST_DATABASE") ?? throw new InvalidOperationException("Configure the disposable AMORRP_TEST_DATABASE.");
        var b = new NpgsqlConnectionStringBuilder(configured);
        if (b.Database?.StartsWith("amorrp_test", StringComparison.Ordinal) != true) throw new InvalidOperationException("Refusing a non-test database.");
        adminConnection = b.ConnectionString; database = "amorrp_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin); await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        b.Database = database; Connection = b.ConnectionString;
    }
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin); await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
