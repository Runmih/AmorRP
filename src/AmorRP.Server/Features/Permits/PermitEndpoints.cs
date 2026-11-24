using AmorRP.Contracts.Permits;

namespace AmorRP.Server.Features.Permits;

public static class PermitEndpoints
{
    public static void MapPermitEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/permits", (HttpContext h, Guid seasonId, PermitService s, CancellationToken ct) => s.ReadAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/permits/policy", (HttpContext h, Guid seasonId, PermitPolicy b, PermitService s, CancellationToken ct) => s.PolicyAsync(h, seasonId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/permits/listings", (HttpContext h, Guid seasonId, PermitListingEdit b, PermitService s, CancellationToken ct) => s.NominateAsync(h, seasonId, b, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/permits/listings/mine", (HttpContext h, Guid seasonId, PermitListingConsent b, PermitService s, CancellationToken ct) => s.ListingConsentAsync(h, seasonId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/permits/purchases", (HttpContext h, Guid seasonId, PermitPurchase b, PermitService s, CancellationToken ct) => s.BuyAsync(h, seasonId, b, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/permits/offers", (HttpContext h, Guid seasonId, PermitOfferCreate b, PermitService s, CancellationToken ct) => s.OfferAsync(h, seasonId, b, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/permits/offers/{offerId}/accept", (HttpContext h, Guid seasonId, Guid offerId, PermitQuoteAction b, PermitService s, CancellationToken ct) => s.AcceptAsync(h, seasonId, offerId, b, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/permits/offers/{offerId}", (HttpContext h, Guid seasonId, Guid offerId, PermitService s, CancellationToken ct) => s.WithdrawAsync(h, seasonId, offerId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/permits/{permitId}/transfers", (HttpContext h, Guid seasonId, Guid permitId, PermitTransferCreate body, PermitService s, CancellationToken ct) =>
            s.OfferTransferAsync(h, seasonId, permitId, body, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/permit-transfers/{transferId}/approval", (HttpContext h, Guid seasonId, Guid transferId, PermitService s, CancellationToken ct) =>
            s.DecideTransferAsync(h, seasonId, transferId, "approve", ct));
        app.MapPut("/api/v2/seasons/{seasonId}/permit-transfers/{transferId}/acceptance", (HttpContext h, Guid seasonId, Guid transferId, PermitService s, CancellationToken ct) =>
            s.DecideTransferAsync(h, seasonId, transferId, "accept", ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/permit-transfers/{transferId}", (HttpContext h, Guid seasonId, Guid transferId, PermitService s, CancellationToken ct) =>
            s.DecideTransferAsync(h, seasonId, transferId, "cancel", ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/permits/{permitId}", (HttpContext h, Guid seasonId, Guid permitId, PermitService s, CancellationToken ct) => s.ReleaseAsync(h, seasonId, permitId, ct));
    }
}
