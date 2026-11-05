using AmorRP.Contracts.Currency;
namespace AmorRP.Server.Features.Billing;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/communities/{communityId}/billing-policy", (HttpContext h, Guid communityId, BillingService s, CancellationToken ct) => s.PolicyAsync(h, communityId, null, ct));
        app.MapPut("/api/v2/communities/{communityId}/billing-policy", (HttpContext h, Guid communityId, BillingPolicyEdit b, BillingService s, CancellationToken ct) => s.PolicyAsync(h, communityId, b, ct));
        app.MapPost("/api/v2/communities/{communityId}/groups/{groupId}/bills", (HttpContext h, Guid communityId, Guid groupId, BillCreate b, BillingService s, CancellationToken ct) => s.CreateAsync(h, communityId, groupId, b, ct));
        app.MapGet("/api/v2/communities/{communityId}/groups/{groupId}/bills", (HttpContext h, Guid communityId, Guid groupId, BillingService s, CancellationToken ct) => s.ListAsync(h, communityId, groupId, ct));
        app.MapGet("/api/v2/communities/{communityId}/bills/mine", (HttpContext h, Guid communityId, BillingService s, CancellationToken ct) => s.ListAsync(h, communityId, null, ct));
        app.MapGet("/api/v2/communities/{communityId}/bills/{billId}/payments", (HttpContext h, Guid communityId, Guid billId, BillingService s, CancellationToken ct) => s.PaymentsAsync(h, communityId, billId, ct));
    }
}
