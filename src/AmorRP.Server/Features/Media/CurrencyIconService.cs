using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Media;

public sealed class CurrencyIconService(AmorDbContext db, SessionAccess sessions, SeasonAccess communities,
    CommandStore commands, IOptions<ServicePolicyOptions> policy)
{
    public async Task<IResult> ReadAsync(HttpContext h, Guid communityId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; var (community, _) = await communities.RequireAsync(communityId, actor, ct);
        ApiFault.Require(community.CurrencyIconAssetId != null, 404, "not_found", "No custom currency icon.");
        h.Response.Headers.ETag = CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion);
        return await MediaAssets.ReadAsync(h, db, communityId, community.CurrencyIconAssetId!.Value, "currency", "The currency icon changed. Refresh to load its latest version.", ct);
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid communityId, bool upload, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        // Authorize before parsing untrusted media; authorize again under the write lock.
        var (initial, _) = await communities.RequireAsync(communityId, actor, ct); SeasonAccess.Owner(initial, actor);
        var image = upload ? await StaticImageUpload.ReadAsync(h, policy.Value.MaxCurrencyIconBytes, 128, 128, 131072, ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        // The preflight may have tracked an outdated owner/version; reload under the lock.
        await db.Entry(initial).ReloadAsync(ct);
        var (community, _) = await communities.RequireAsync(communityId, actor, ct); SeasonAccess.Owner(community, actor);
        var body = MediaAssets.Fingerprint(image);
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion));
        var previous = community.CurrencyIconAssetId;
        community.CurrencyIconAssetId = MediaAssets.Add(db, communityId, image, "currency");
        community.CurrencyVersion++;
        var op = commands.New(h, scope, body, actor, communityId, upload ? "currency.icon.replace" : "currency.icon.remove"); op.Summary = upload ? "Updated currency icon." : "Removed custom currency icon.";
        h.Response.Headers.ETag = CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion);
        var result = await commands.SaveAsync(h, op, new CommandResult<AmorRP.Contracts.Currency.Currency>(op.Id, SeasonAccess.Currency(community)), ct);
        await MediaAssets.RemoveAsync(db, communityId, previous, "currency", ct);
        await tx.CommitAsync(ct); return result;
    }
}
