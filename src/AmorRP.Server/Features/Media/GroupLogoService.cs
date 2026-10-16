using System.Security.Cryptography;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace AmorRP.Server.Features.Media;

public sealed class GroupLogoService(AmorDbContext db, SessionAccess sessions, GroupAccess groups,
    CommandStore commands, IOptions<ServicePolicyOptions> policy, TimeProvider time)
{
    public async Task<IResult> ReadAsync(HttpContext h, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (group, _) = await groups.RequireAsync(groupId, actor, ct);
        ApiFault.Require(group.LogoAssetId != null, 404, "not_found", "No group logo.");
        var asset = await db.MediaAssets.SingleAsync(x => x.GroupId == groupId && x.Id == group.LogoAssetId && x.Purpose == "group_logo", ct);
        h.Response.Headers.ETag = CommandStore.ETag("group-logo", groupId, group.LogoVersion);
        h.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Bytes(asset.Png, "image/png");
    }
    public async Task<IResult> ChangeAsync(HttpContext h, Guid groupId, bool upload, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (initial, _) = await groups.RequireAsync(groupId, actor, ct); GroupAccess.Owner(initial, actor);
        var image = upload ? await StaticImageUpload.ReadAsync(h, policy.Value.MaxGroupLogoBytes, 2048, 512, 1048576, ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "group:" + groupId], ct);
        await db.Entry(initial).ReloadAsync(ct);
        var (group, member) = await groups.RequireAsync(groupId, actor, ct); GroupAccess.Owner(group, actor);
        object? body = image == null ? null : new { contentHash = Convert.ToHexString(SHA256.HashData(image.Input)) };
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("group-logo", groupId, group.LogoVersion));
        var previous = group.LogoAssetId;
        if (image != null)
        {
            var asset = new MediaAssetRow { GroupId = groupId, Purpose = "group_logo", Png = image.Png,
                Width = image.Width, Height = image.Height, ContentHash = Convert.ToHexString(SHA256.HashData(image.Png)) };
            db.MediaAssets.Add(asset); group.LogoAssetId = asset.Id;
        }
        else group.LogoAssetId = null;
        group.LogoVersion++;
        var op = commands.New(h, scope, body, actor, groupId, upload ? "group.logo.replace" : "group.logo.remove");
        op.Summary = upload ? "Updated group logo." : "Removed group logo.";
        h.Response.Headers.ETag = CommandStore.ETag("group-logo", groupId, group.LogoVersion);
        var result = await commands.SaveAsync(h, op, new CommandResult<Group>(op.Id, await groups.ViewAsync(group, member, time.GetUtcNow(), ct)), ct);
        if (previous.HasValue) await db.MediaAssets.Where(x => x.GroupId == groupId && x.Id == previous && x.Purpose == "group_logo").ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct); return result;
    }
}
