using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AmorRP.Server.Features.Media;

public sealed class SeasonLogoService(AmorDbContext db, SessionAccess sessions, SeasonAccess communities,
    CommandStore commands, IOptions<ServicePolicyOptions> policy, TimeProvider time)
{
    public async Task<IResult> ReadAsync(HttpContext h, Guid communityId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (community, _) = await communities.RequireAsync(communityId, actor, ct);
        ApiFault.Require(community.LogoAssetId != null, 404, "not_found", "No season logo.");
        h.Response.Headers.ETag = CommandStore.ETag("season-logo", communityId, community.LogoVersion);
        return await MediaAssets.ReadAsync(h, db, communityId, community.LogoAssetId!.Value, "community_logo", "The season logo changed. Refresh to load its latest version.", ct);
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid communityId, bool upload, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (initial, _) = await communities.RequireAsync(communityId, actor, ct); SeasonAccess.Owner(initial, actor);
        var image = upload ? await StaticImageUpload.ReadAsync(h, policy.Value.MaxCommunityLogoBytes, 2048, 512, 1048576, ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        await db.Entry(initial).ReloadAsync(ct);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct); SeasonAccess.Owner(community, actor);
        var body = MediaAssets.Fingerprint(image);
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("season-logo", communityId, community.LogoVersion));
        var previous = community.LogoAssetId;
        community.LogoAssetId = MediaAssets.Add(db, communityId, image, "community_logo");
        community.LogoVersion++;
        var op = commands.New(h, scope, body, actor, communityId, upload ? "season.logo.replace" : "season.logo.remove");
        op.Summary = upload ? "Updated season logo." : "Removed season logo.";
        h.Response.Headers.ETag = CommandStore.ETag("season-logo", communityId, community.LogoVersion);
        var result = await commands.SaveAsync(h, op, new CommandResult<Season>(op.Id, await communities.ViewAsync(community, member, time.GetUtcNow(), ct)), ct);
        await MediaAssets.RemoveAsync(db, communityId, previous, "community_logo", ct);
        await tx.CommitAsync(ct); return result;
    }
}
