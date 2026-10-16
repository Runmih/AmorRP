using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
namespace AmorRP.Server.Features.Authentication;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/assets/login-background-{contentHash}.jpg", (HttpContext h, string contentHash) => Browser.LoginResultPage.Background(h, contentHash));
        app.MapPost("/api/v1/auth/login-attempts", (HttpContext h, LoginStart b, AuthenticationService s, CancellationToken ct) => s.StartAsync(h, b, ct)).RequireRateLimiting("identity-probe");
        app.MapGet("/api/v1/auth/login-attempts/{attemptId}", (HttpContext h, Guid attemptId, AuthenticationService s, CancellationToken ct) => s.StatusAsync(h, attemptId, ct)).RequireRateLimiting("identity-probe");
        app.MapDelete("/api/v1/auth/login-attempts/{attemptId}", (HttpContext h, Guid attemptId, AuthenticationService s, CancellationToken ct) => s.ExchangeAsync(h, attemptId, true, ct)).RequireRateLimiting("identity-probe");
        app.MapPost("/api/v1/auth/login-attempts/{attemptId}/exchange", (HttpContext h, Guid attemptId, LoginExchangeRequest body, AuthenticationService s, CancellationToken ct) => s.ExchangeAsync(h, attemptId, false, ct, body)).RequireRateLimiting("identity-probe");
        app.MapGet("/auth/xivauth/callback", async (HttpContext h, AuthenticationService s, CancellationToken ct) =>
        {
            try { return await s.CallbackAsync(h, ct); }
            catch (Infrastructure.Security.ApiFault fault)
            { return Browser.LoginResultPage.Render(h, false, fault.Status); }
        }).RequireRateLimiting("identity-probe");
        app.MapPost("/api/v1/auth/sessions/refresh", (HttpContext h, RefreshRequest b, AuthenticationService s, CancellationToken ct) => s.RefreshAsync(h, b, ct)).RequireRateLimiting("identity-probe");
        app.MapDelete("/api/v1/auth/sessions/current", (HttpContext h, AuthenticationService s, CancellationToken ct) => s.RevokeAsync(h, null, ct));
        app.MapGet("/api/v1/me", (HttpContext h, AuthenticationService s, CancellationToken ct) => s.MeAsync(h, ct));
        app.MapGet("/api/v1/me/sessions", (HttpContext h, AuthenticationService s, CancellationToken ct) => s.SessionsAsync(h, ct));
        app.MapDelete("/api/v1/me/sessions/{sessionId}", (HttpContext h, Guid sessionId, AuthenticationService s, CancellationToken ct) => s.RevokeAsync(h, sessionId, ct));
    }
}
