using System.Security.Cryptography;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Media;

public sealed class CurrencyIconService(AmorDbContext db, SessionAccess sessions, GroupAccess groups,
    CommandStore commands, IOptions<ServicePolicyOptions> policy)
{
    public async Task<IResult> ReadAsync(HttpContext h, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; var (group, _) = await groups.RequireAsync(groupId, actor, ct);
        ApiFault.Require(group.CurrencyIconAssetId != null, 404, "not_found", "No custom currency icon.");
        var asset = await db.MediaAssets.SingleAsync(x => x.GroupId == groupId && x.Id == group.CurrencyIconAssetId, ct);
        h.Response.Headers.ETag = CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion);
        return Results.Bytes(asset.Png, "image/png");
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid groupId, bool upload, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        // Authorize before parsing untrusted media; authorize again under the write lock.
        var (initial, _) = await groups.RequireAsync(groupId, actor, ct); GroupAccess.Owner(initial, actor);
        var image = upload ? await StaticImageUpload.ReadAsync(h, policy.Value.MaxCurrencyIconBytes, 128, 128, 131072, ct) : null;
        var input = image?.Input; var png = image?.Png;
        var width = image?.Width ?? 0; var height = image?.Height ?? 0;
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CommandStore.LockAsync(db, ["character:" + actor, "group:" + groupId], ct);
        // The preflight may have tracked an outdated owner/version; reload under the lock.
        await db.Entry(initial).ReloadAsync(ct);
        var (group, _) = await groups.RequireAsync(groupId, actor, ct); GroupAccess.Owner(group, actor);
        object? body = input == null ? null : new { contentHash = Convert.ToHexString(SHA256.HashData(input)) };
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion));
        var previous = group.CurrencyIconAssetId;
        if (png != null)
        {
            var asset = new MediaAssetRow { GroupId = groupId, Png = png, Width = width, Height = height, ContentHash = Convert.ToHexString(SHA256.HashData(png)) };
            db.MediaAssets.Add(asset); group.CurrencyIconAssetId = asset.Id;
        }
        else group.CurrencyIconAssetId = null;
        group.CurrencyVersion++;
        var op = commands.New(h, scope, body, actor, groupId, upload ? "currency.icon.replace" : "currency.icon.remove"); op.Summary = upload ? "Updated currency icon." : "Removed custom currency icon.";
        h.Response.Headers.ETag = CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion);
        var result = await commands.SaveAsync(h, op, new CommandResult<AmorRP.Contracts.Currency.Currency>(op.Id, GroupAccess.Currency(group)), ct);
        if (previous.HasValue) { await db.MediaAssets.Where(x => x.GroupId == groupId && x.Id == previous).ExecuteDeleteAsync(ct); }
        await tx.CommitAsync(ct); return result;
    }
}
