using System.Security.Cryptography;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AmorRP.Server.Features.Media;

public sealed class CommunityLogoService(AmorDbContext db, SessionAccess sessions, CommunityAccess communities,
    CommandStore commands, IOptions<ServicePolicyOptions> policy, TimeProvider time)
{
    public async Task<IResult> ReadAsync(HttpContext h, Guid communityId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (community, _) = await communities.RequireAsync(communityId, actor, ct);
        ApiFault.Require(community.LogoAssetId != null, 404, "not_found", "No community logo.");
        var asset = await db.MediaAssets.SingleAsync(x => x.CommunityId == communityId && x.Id == community.LogoAssetId && x.Purpose == "community_logo", ct);
        h.Response.Headers.ETag = CommandStore.ETag("community-logo", communityId, community.LogoVersion);
        h.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Bytes(asset.Png, "image/png");
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid communityId, bool upload, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (initial, _) = await communities.RequireAsync(communityId, actor, ct); CommunityAccess.Owner(initial, actor);
        var image = upload ? await StaticImageUpload.ReadAsync(h, policy.Value.MaxCommunityLogoBytes, 2048, 512, 1048576, ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        await db.Entry(initial).ReloadAsync(ct);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct); CommunityAccess.Owner(community, actor);
        object? body = image == null ? null : new { contentHash = Convert.ToHexString(SHA256.HashData(image.Input)) };
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("community-logo", communityId, community.LogoVersion));
        var previous = community.LogoAssetId;
        if (image != null)
        {
            var asset = new MediaAssetRow { CommunityId = communityId, Purpose = "community_logo", Png = image.Png,
                Width = image.Width, Height = image.Height, ContentHash = Convert.ToHexString(SHA256.HashData(image.Png)) };
            db.MediaAssets.Add(asset); community.LogoAssetId = asset.Id;
        }
        else community.LogoAssetId = null;
        community.LogoVersion++;
        var op = commands.New(h, scope, body, actor, communityId, upload ? "community.logo.replace" : "community.logo.remove");
        op.Summary = upload ? "Updated community logo." : "Removed community logo.";
        h.Response.Headers.ETag = CommandStore.ETag("community-logo", communityId, community.LogoVersion);
        var result = await commands.SaveAsync(h, op, new CommandResult<Community>(op.Id, await communities.ViewAsync(community, member, time.GetUtcNow(), ct)), ct);
        if (previous.HasValue) await db.MediaAssets.Where(x => x.CommunityId == communityId && x.Id == previous && x.Purpose == "community_logo").ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct); return result;
    }
}
