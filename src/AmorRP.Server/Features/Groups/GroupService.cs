using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Features.Media;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Groups;

public sealed class GroupService(AmorDbContext db, SessionAccess sessions, CommunityAccess communities, CommandStore commands, PageCursor pages, IOptions<ServicePolicyOptions> policy)
{
    private readonly HashSet<Guid> joined = [], favorites = [];
    private async Task LoadViewerAsync(Guid actor, Guid communityId, CancellationToken ct)
    {
        joined.UnionWith(await db.GroupMemberships.Where(x => x.CommunityId == communityId && x.CharacterId == actor).Select(x => x.GroupId).ToArrayAsync(ct));
        favorites.UnionWith(await db.GroupFavorites.Where(x => x.CommunityId == communityId && x.CharacterId == actor).Select(x => x.GroupId).ToArrayAsync(ct));
    }
    private Group View(GroupRow row) => new(row.Id, row.CommunityId, row.Name, row.Description, row.LogoAssetId, row.LogoVersion, row.Version, CommandStore.ETag("group", row.Id, row.Version), row.OwnerCharacterId, row.JoinMode, joined.Contains(row.Id), favorites.Contains(row.Id));
    private async Task<GroupRow> RequireAsync(Guid communityId, Guid groupId, CancellationToken ct)
    {
        var row = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == groupId && !x.Deleted, ct);
        ApiFault.Require(row != null, 404, "not_found", "Group was not found in this community."); return row!;
    }
    public async Task<IResult> ListAsync(HttpContext h, Guid communityId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await communities.RequireAsync(communityId, actor, ct); await LoadViewerAsync(actor, communityId, ct);
        var scope = $"community-groups:{communityId}:{actor}"; var (limit, after) = pages.Read(h, scope);
        var query = db.Groups.AsNoTracking().Where(x => x.CommunityId == communityId && !x.Deleted);
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        return Results.Json(new GroupPage(rows.Take(limit).Select(View).ToArray(), rows.Length > limit ? pages.Write(rows[limit - 1].Id, scope) : null));
    }
    public async Task<IResult> ReadAsync(HttpContext h, Guid communityId, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await communities.RequireAsync(communityId, actor, ct);
        await LoadViewerAsync(actor, communityId, ct);
        var row = await RequireAsync(communityId, groupId, ct); h.Response.Headers.ETag = View(row).Etag; return Results.Json(View(row));
    }
    public async Task<IResult> WriteAsync(HttpContext h, Guid communityId, Guid? groupId, GroupWrite? body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct); if (groupId == null) CommunityAccess.Action(community, member, "groups.create");
        await LoadViewerAsync(actor, communityId, ct);
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        GroupRow row;
        if (!groupId.HasValue)
        {
            ApiFault.Require(body != null, 422, "invalid_request", "Enter a group name and description.");
            ApiFault.Require(await db.Groups.CountAsync(x => x.CommunityId == communityId, ct) < 100, 409, "group_limit_reached", "This community has reached its group creation limit.");
            ApiFault.Require(await db.Groups.CountAsync(x => x.OwnerCharacterId == actor && !x.Deleted && db.Communities.Any(c => c.Id == x.CommunityId && !c.Deleted), ct) < 3, 409, "owned_groups_limit", "You can own at most 3 groups across all communities.");
            row = new GroupRow { CommunityId = communityId, OwnerCharacterId = actor }; db.Groups.Add(row);
            db.GroupMemberships.Add(new() { CommunityId = communityId, GroupId = row.Id, CharacterId = actor }); joined.Add(row.Id);
        }
        else {
            row = await RequireAsync(communityId, groupId.Value, ct);
            if (body == null && row.OwnerCharacterId != actor) CommunityAccess.Action(community, member, "groups.manage");
            else GroupActivityService.Owner(row, actor);
            CommandStore.Match(h, View(row).Etag);
        }
        var deleting = body == null;
        if (deleting) row.Deleted = true;
        else
        {
            var nameError = PlainText.Error(body!.Name, 80, required: true) ?? (body.Name.Length > 80 ? "Group name must be 80 characters or fewer." : null);
            var descriptionError = PlainText.Error(body.Description, 300, multiline: true, required: false);
            ApiFault.Require(nameError == null && descriptionError == null, 422, "invalid_description", nameError ?? descriptionError ?? "Check the group profile.");
            ApiFault.Require(body.JoinMode is "open" or "closed" or "password", 422, "invalid_join_mode", "Choose Open, Closed or Password protected.");
            ApiFault.Require(body.Password == null || body.Password.Length is > 0 and <= 64, 422, "invalid_password", "Group passwords must contain 1–64 characters.");
            if (body.Password != null) row.PasswordHash = new PasswordHasher<GroupRow>().HashPassword(row, body.Password);
            ApiFault.Require(body.JoinMode != "password" || row.PasswordHash != null, 422, "invalid_password", "Set a password before protecting this group.");
            row.JoinMode = body.JoinMode; if (row.JoinMode != "password") row.PasswordHash = null;
            row.Name = body.Name.Trim(); row.Description = body.Description ?? "";
        }
        row.Version++; community.Version++;
        var op = commands.New(h, scope, body, actor, communityId, deleting ? "group.delete" : groupId.HasValue ? "group.edit" : "group.create");
        op.Summary = deleting ? "Group deleted." : "Group profile saved.";
        var previous = deleting ? row.LogoAssetId : null;
        if (deleting) { row.LogoAssetId = null; row.LogoVersion++; }
        var result = await commands.SaveAsync(h, op, new CommandResult<Group>(op.Id, View(row)), ct);
        if (previous.HasValue) await db.MediaAssets.Where(x => x.CommunityId == communityId && x.Id == previous && x.Purpose == "group_logo").ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> LogoAsync(HttpContext h, Guid communityId, Guid groupId, bool? upload, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (initial, initialMember) = await communities.RequireAsync(communityId, actor, ct);
        if (upload == null)
        {
            var row = await RequireAsync(communityId, groupId, ct); ApiFault.Require(row.LogoAssetId != null, 404, "not_found", "No group logo yet.");
            var image = await db.MediaAssets.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == row.LogoAssetId && x.Purpose == "group_logo", ct);
            ApiFault.Require(image != null, 404, "not_found", "The group logo changed. Refresh to load its latest version.");
            h.Response.Headers["X-Content-Type-Options"] = "nosniff"; return Results.Bytes(image!.Png, "image/png");
        }
        GroupActivityService.Owner(await RequireAsync(communityId, groupId, ct), actor);
        var incoming = upload.Value ? await StaticImageUpload.ReadAsync(h, policy.Value.MaxCommunityLogoBytes, 2048, 512, 1048576, ct) : null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        await db.Entry(initial).ReloadAsync(ct);
        await db.Entry(initialMember).ReloadAsync(ct);
        var (community, member) = await communities.RequireAsync(communityId, actor, ct);
        var target = await RequireAsync(communityId, groupId, ct); GroupActivityService.Owner(target, actor); await LoadViewerAsync(actor, communityId, ct);
        object? body = incoming == null ? null : new { contentHash = Convert.ToHexString(SHA256.HashData(incoming.Input)) };
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("group-logo", groupId, target.LogoVersion)); var previous = target.LogoAssetId;
        if (incoming != null)
        {
            var asset = new MediaAssetRow { CommunityId = communityId, Purpose = "group_logo", Png = incoming.Png, Width = incoming.Width, Height = incoming.Height, ContentHash = Convert.ToHexString(SHA256.HashData(incoming.Png)) };
            db.MediaAssets.Add(asset); target.LogoAssetId = asset.Id;
        }
        else target.LogoAssetId = null;
        target.LogoVersion++; target.Version++; community.Version++;
        var op = commands.New(h, scope, body, actor, communityId, upload.Value ? "group.logo.replace" : "group.logo.remove");
        h.Response.Headers.ETag = CommandStore.ETag("group-logo", groupId, target.LogoVersion);
        var result = await commands.SaveAsync(h, op, new CommandResult<Group>(op.Id, View(target)), ct);
        if (previous.HasValue) await db.MediaAssets.Where(x => x.CommunityId == communityId && x.Id == previous && x.Purpose == "group_logo").ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct); return result;
    }
}
