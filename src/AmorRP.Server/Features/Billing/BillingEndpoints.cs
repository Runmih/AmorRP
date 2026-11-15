using AmorRP.Contracts.Currency;
namespace AmorRP.Server.Features.Billing;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/billing-policy", (HttpContext h, Guid seasonId, BillingService s, CancellationToken ct) => s.PolicyAsync(h, seasonId, null, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/billing-policy", (HttpContext h, Guid seasonId, BillingPolicyEdit b, BillingService s, CancellationToken ct) => s.PolicyAsync(h, seasonId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/bills", (HttpContext h, Guid seasonId, Guid groupId, BillCreate b, BillingService s, CancellationToken ct) => s.CreateAsync(h, seasonId, groupId, b, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/bills", (HttpContext h, Guid seasonId, Guid groupId, BillingService s, CancellationToken ct) => s.ListAsync(h, seasonId, groupId, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/bills/mine", (HttpContext h, Guid seasonId, BillingService s, CancellationToken ct) => s.ListAsync(h, seasonId, null, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/bills/{billId}/payments", (HttpContext h, Guid seasonId, Guid billId, BillingService s, CancellationToken ct) => s.PaymentsAsync(h, seasonId, billId, ct));
    }
}
