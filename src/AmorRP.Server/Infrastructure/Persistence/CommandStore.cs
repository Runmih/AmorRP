using System.Text.Json;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

// Locks are database-wide across server replicas, never process-local semaphores.
public sealed class CommandStore(AmorDbContext db, SecretVault vault, TimeProvider time)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task LockAsync(AmorDbContext db, IEnumerable<string> resources, CancellationToken ct)
    {
        foreach (var name in resources.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({name}, 0))", ct);
    }
    public async Task<OperationRow?> ReplayAsync(HttpContext context, string scope, object? body, CancellationToken ct)
    {
        var text = context.Request.Headers["Idempotency-Key"].ToString();
        ApiFault.Require(Guid.TryParse(text, out var key) && key.Version == 7, 400, "invalid_operation_key", "A UUIDv7 Idempotency-Key is required.");
        var hash = SecretVault.Hash(context.Request.Method + " " + context.Request.Path + "\n" + context.Request.Headers.IfMatch + "\n" + JsonSerializer.Serialize(body, Json));
        var prior = await db.Operations.SingleOrDefaultAsync(x => x.Scope == scope && x.Key == key, ct);
        if (prior != null)
        {
            ApiFault.Require(prior.RequestHash == hash, 409, "idempotency_mismatch", "Operation key was used for a different request.");
            ApiFault.Require(prior.OccurredAt.AddDays(7) > time.GetUtcNow(), 409, "operation_replay_expired", "Operation replay has expired.");
            context.Response.Headers["Idempotency-Replayed"] = "true";
            if (prior.ETag != null) context.Response.Headers.ETag = prior.ETag;
            return prior;
        }
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(key.ToString("N")[..12], 16));
        ApiFault.Require(timestamp > time.GetUtcNow().AddDays(-1) && timestamp < time.GetUtcNow().AddMinutes(5),
            409, "operation_replay_expired", "Operation key is outside its submission window.");
        return null;
    }
    public IResult Replay(OperationRow row) => Results.Content(vault.Unprotect(row.ProtectedResponse, "operation:" + row.Id), "application/json", statusCode: row.ResponseStatus);
    public OperationRow New(HttpContext context, string scope, object? body, Guid? actor, Guid? group, string kind) => new()
    {
        Scope = scope, Key = Guid.Parse(context.Request.Headers["Idempotency-Key"].ToString()),
        RequestHash = SecretVault.Hash(context.Request.Method + " " + context.Request.Path + "\n" + context.Request.Headers.IfMatch + "\n" + JsonSerializer.Serialize(body, Json)),
        ActorCharacterId = actor, GroupId = group, Kind = kind, Summary = kind, OccurredAt = time.GetUtcNow()
    };
    public async Task<IResult> SaveAsync(HttpContext context, OperationRow operation, object response, CancellationToken ct)
    {
        if (operation.Kind is "announcement.create" or "group.create" or "invitation.create" or "ownership.propose" or "login.start" or "category.create" or "definition.create" or "potion.create" or "letter.create") operation.ResponseStatus = 201;
        var json = JsonSerializer.Serialize(response, Json);
        operation.ProtectedResponse = vault.Protect(json, "operation:" + operation.Id);
        operation.ETag = context.Response.Headers.ETag;
        db.Operations.Add(operation);
        await db.SaveChangesAsync(ct);
        return Results.Content(json, "application/json", statusCode: operation.ResponseStatus);
    }
    public static void Match(HttpContext context, string etag)
    {
        var requested = context.Request.Headers.IfMatch.ToString();
        ApiFault.Require(requested.Length > 0, 428, "precondition_required", "Refresh the resource and supply its ETag.");
        ApiFault.Require(requested == etag, 412, "version_mismatch", "Resource changed. Refresh before editing.");
    }
    public static string ETag(string kind, Guid id, int version) => $"\"{kind}:{id:N}:{version}\"";
}
