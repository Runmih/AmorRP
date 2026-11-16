using System.Security.Cryptography;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Media;

// Shared storage only. Callers retain authorization, locks, versions and receipt ordering.
internal static class MediaAssets
{
    public static object? Fingerprint(StaticImageUpload.Image? image) => image == null ? null
        : new { contentHash = Convert.ToHexString(SHA256.HashData(image.Input)) };

    public static Guid? Add(AmorDbContext db, Guid seasonId, StaticImageUpload.Image? image, string purpose)
    {
        if (image == null) return null;
        var asset = new MediaAssetRow { CommunityId = seasonId, Purpose = purpose, Png = image.Png,
            Width = image.Width, Height = image.Height, ContentHash = Convert.ToHexString(SHA256.HashData(image.Png)) };
        db.MediaAssets.Add(asset);
        return asset.Id;
    }

    public static Task RemoveAsync(AmorDbContext db, Guid seasonId, Guid? assetId, string purpose, CancellationToken ct) =>
        assetId.HasValue ? db.MediaAssets.Where(x => x.CommunityId == seasonId && x.Id == assetId && x.Purpose == purpose).ExecuteDeleteAsync(ct)
            : Task.CompletedTask;

    public static async Task<IResult> ReadAsync(HttpContext http, AmorDbContext db, Guid seasonId, Guid assetId, string purpose, string missingMessage, CancellationToken ct)
    {
        var asset = await db.MediaAssets.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.Id == assetId && x.Purpose == purpose, ct);
        ApiFault.Require(asset != null, 404, "not_found", missingMessage);
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Bytes(asset!.Png, "image/png");
    }
}
