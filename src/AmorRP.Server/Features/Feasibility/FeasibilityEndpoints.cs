using AmorRP.Contracts.Feasibility;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Feasibility;

public static class FeasibilityEndpoints
{
    public static void MapFeasibilityEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/feasibility/login-attempts", (IdentityProbeStart selected, HttpContext context,
            IOptions<XivAuthOptions> options, IdentityProbeStore store, ICharacterIdentityProvider provider) =>
        {
            NoStore(context);
            if (!options.Value.Enabled) return Disabled();
            var credential = context.Request.Headers["X-Login-Request-Credential"].ToString();
            if (!Key(context, out var key) || !IdentityProbeStore.ValidSecret(credential)
                || !Text(selected.DisplayName, 80) || !Text(selected.HomeWorld, 80)) return Rejected(400);
            var created = store.Start(key, credential, selected, provider);
            return created is null ? Rejected(409) : Results.Json(created, statusCode: 201);
        }).RequireRateLimiting("identity-probe");

        app.MapGet("/api/v1/feasibility/login-attempts/{attemptId}", (Guid attemptId, HttpContext context,
            IOptions<XivAuthOptions> options, IdentityProbeStore store) =>
        {
            NoStore(context);
            if (!options.Value.Enabled) return Disabled();
            var status = store.Status(attemptId, Credential(context));
            return status is null ? Rejected(401) : Results.Ok(status);
        }).RequireRateLimiting("identity-probe");

        app.MapGet("/auth/xivauth/feasibility-callback", async (HttpContext context, IOptions<XivAuthOptions> options,
            IdentityProbeStore store, ICharacterIdentityProvider provider) =>
        {
            NoStore(context);
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (!options.Value.Enabled) return Disabled();
            var state = context.Request.Query["state"].ToString();
            var code = context.Request.Query["code"].ToString();
            if (!IdentityProbeStore.ValidSecret(state) || code.Length > 2048) return Rejected(400);
            var success = await store.CompleteAsync(state, context.Request.Query.ContainsKey("error") ? null : code, provider, context.RequestAborted);
            return Results.Content(success
                ? "<!doctype html><title>Amor RP</title><p>Character verified. Return to Amor RP in the game.</p>"
                : "<!doctype html><title>Amor RP</title><p>Login failed or expired. Return to the game and start again.</p>",
                "text/html", statusCode: success ? 200 : 400);
        }).RequireRateLimiting("identity-probe");

        app.MapPost("/api/v1/feasibility/login-attempts/{attemptId}/exchange", (Guid attemptId, HttpContext context,
            IOptions<XivAuthOptions> options, IdentityProbeStore store) =>
        {
            NoStore(context);
            if (!options.Value.Enabled) return Disabled();
            if (!Key(context, out var key)) return Rejected(400);
            var session = store.Exchange(attemptId, Credential(context), key);
            return session is null ? Rejected(401) : Results.Ok(session);
        }).RequireRateLimiting("identity-probe");

        app.MapGet("/api/v1/feasibility/identity", (HttpContext context, IOptions<XivAuthOptions> options, IdentityProbeStore store) =>
        {
            NoStore(context);
            if (!options.Value.Enabled) return Disabled();
            var character = store.Identity(Bearer(context));
            return character is null ? Rejected(401) : Results.Ok(character);
        }).RequireRateLimiting("identity-probe");

        app.MapDelete("/api/v1/feasibility/session", (HttpContext context, IOptions<XivAuthOptions> options, IdentityProbeStore store) =>
        {
            NoStore(context);
            if (!options.Value.Enabled) return Disabled();
            if (!Key(context, out _)) return Rejected(400);
            store.Logout(Bearer(context));
            return Results.NoContent();
        }).RequireRateLimiting("identity-probe");
    }

    private static IResult Disabled() => Results.Problem(statusCode: 503, title: "XIVAuth is not configured. Follow the M1 setup guide.");
    private static IResult Rejected(int status) => Results.Problem(statusCode: status, title: "Identity probe request rejected.");
    private static void NoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";
    private static bool Text(string? value, int limit) => !string.IsNullOrWhiteSpace(value) && value.Length <= limit && !value.Any(char.IsControl);
    private static string Credential(HttpContext context) => context.Request.Headers["X-Login-Attempt-Credential"].ToString();
    private static string Bearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) && IdentityProbeStore.ValidSecret(header[7..]) ? header[7..] : "";
    }
    private static bool Key(HttpContext context, out Guid key)
    {
        var header = context.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(header, "D", out key) || key.Version != 7) return false;
        // Spike replay state lives only 20 minutes. Old keys must never become new attempts.
        var timestamp = Convert.ToInt64(key.ToString("N")[..12], 16);
        if (timestamp > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()) return false;
        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
        return age >= TimeSpan.FromMinutes(-1) && age <= TimeSpan.FromMinutes(10);
    }
}
