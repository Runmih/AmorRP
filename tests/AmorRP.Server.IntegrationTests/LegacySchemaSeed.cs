using System.Data;
using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AmorRP.Server.IntegrationTests;

public sealed partial class FoundationTests
{
    // Upgrade tests seed the schema as it existed at their target migration,
    // without making the current domain model pretend that communities are groups.
    private static async Task LegacyInsert<T>(AmorDbContext db, T row, CancellationToken ct) where T : class
    {
        var entity = db.Model.FindEntityType(typeof(T))!;
        var table = entity.GetTableName()!; if (table == "communities") table = "groups";
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table";
        var parameter = schema.CreateParameter(); parameter.ParameterName = "table"; parameter.Value = table; schema.Parameters.Add(parameter);
        var available = new HashSet<string>();
        await using (var reader = await schema.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) available.Add(reader.GetString(0));
        await using var command = connection.CreateCommand();
        var names = new List<string>(); var values = new List<string>();
        foreach (var property in entity.GetProperties())
        {
            var name = property.Name == "CommunityId" ? "GroupId" : property.Name;
            if (!available.Contains(name) || property.PropertyInfo == null) continue;
            var value = property.PropertyInfo.GetValue(row); if (value == null) continue;
            if (name == "Kind" && value is string kind && kind.StartsWith("community.", StringComparison.Ordinal)) value = "group." + kind[10..];
            var p = command.CreateParameter(); p.ParameterName = "p" + values.Count; p.Value = value; command.Parameters.Add(p);
            names.Add('"' + name + '"'); values.Add("@" + p.ParameterName);
        }
        command.CommandText = $"INSERT INTO {table} ({string.Join(",", names)}) VALUES ({string.Join(",", values)})";
        Assert.Equal(1, await command.ExecuteNonQueryAsync(ct));
    }
}
