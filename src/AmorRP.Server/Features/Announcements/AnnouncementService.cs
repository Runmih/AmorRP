using AmorRP.Contracts.Announcements;
using AmorRP.Contracts.Common;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Announcements;

public sealed class AnnouncementService(AmorDbContext db, SessionAccess sessions, CommunityAccess access, CommandStore commands, TimeProvider time)
{
    public async Task<IResult> UnreadAsync(HttpContext h, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var members = await (from m in db.Memberships.AsNoTracking() join g in db.Communities on m.CommunityId equals g.Id
            where m.CharacterId == actor && m.Status == "active" && !g.Deleted select m).ToArrayAsync(ct);
        var ids = members.Select(x => x.CommunityId).ToArray();
        var posts = await db.Announcements.AsNoTracking().Where(x => ids.Contains(x.CommunityId)).Select(x => new { x.CommunityId, x.Id }).ToArrayAsync(ct);
        h.Response.Headers.CacheControl = "no-store";
        return Results.Json(new AnnouncementUnreadPage(members.Select(m => new CommunityAnnouncementUnread(m.CommunityId,
            posts.Count(p => p.CommunityId == m.CommunityId && !m.ReadAnnouncementIds.Contains(p.Id)))).ToArray()));
    }
    public async Task<IResult> ListAsync(HttpContext h, Guid communityId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (_, member) = await access.RequireAsync(communityId, actor, ct);
        var rows = await db.Announcements.AsNoTracking().Where(x => x.CommunityId == communityId).OrderByDescending(x => x.Sequence)
            .Select(x => new { x.Id, x.Title, x.PublishedAt }).Take(AnnouncementCreate.RetainedCount).ToArrayAsync(ct);
        h.Response.Headers.CacheControl = "no-store";
        return Results.Json(new AnnouncementPage(rows.Select(x => new AnnouncementSummary(x.Id, x.Title, x.PublishedAt, !member.ReadAnnouncementIds.Contains(x.Id))).ToArray()));
    }
    public async Task<IResult> ReadAsync(HttpContext h, Guid communityId, Guid announcementId, CancellationToken ct)
    {
        await access.RequireAsync(communityId, (await sessions.GetAsync(h, ct)).CharacterId, ct);
        var row = await db.Announcements.AsNoTracking().SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == announcementId, ct);
        ApiFault.Require(row != null, 404, "not_found", "Announcement is no longer available.");
        var author = await db.Characters.AsNoTracking().SingleAsync(x => x.Id == row!.AuthorCharacterId, ct);
        h.Response.Headers.CacheControl = "no-store";
        return Results.Json(new Announcement(row!.Id, communityId, row.Title, row.Body, SessionAccess.View(author), row.PublishedAt));
    }
    public async Task<IResult> CreateAsync(HttpContext h, Guid communityId, AnnouncementCreate body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var (community, member) = await access.RequireAsync(communityId, actor, ct);
        CommunityAccess.Action(community, member, "announcements.manage");
        var error = PlainText.Error(body.Title, AnnouncementCreate.MaximumTitleCharacters)
            ?? PlainText.Error(body.Body, AnnouncementCreate.MaximumBodyCharacters, multiline: true);
        ApiFault.Require(error == null, 422, "invalid_announcement", error ?? "Invalid announcement.");
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        ApiFault.Require(community.AnnouncementSequence < long.MaxValue, 409, "announcement_limit", "Announcement sequence limit reached.");
        var row = new AnnouncementRow { CommunityId = communityId, AuthorCharacterId = actor, Sequence = ++community.AnnouncementSequence,
            Title = body.Title.Trim(), Body = body.Body, PublishedAt = time.GetUtcNow() };
        var existing = await db.Announcements.Where(x => x.CommunityId == communityId).OrderByDescending(x => x.Sequence).ToArrayAsync(ct);
        db.Announcements.RemoveRange(existing.Skip(AnnouncementCreate.RetainedCount - 1)); db.Announcements.Add(row);
        var retained = existing.Take(AnnouncementCreate.RetainedCount - 1).Select(x => x.Id).ToHashSet();
        member.ReadAnnouncementIds = member.ReadAnnouncementIds.Where(retained.Contains).Append(row.Id).ToArray();
        var op = commands.New(h, scope, body, actor, communityId, "announcement.create"); op.Summary = "Announcement posted.";
        // Keep no body/title in operation receipts: trimming the feed removes message content.
        var response = await commands.SaveAsync(h, op, new Receipt(op.Id), ct);
        await tx.CommitAsync(ct); return response;
    }
    public async Task<IResult> MarkReadAsync(HttpContext h, Guid communityId, Guid announcementId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        ApiFault.Require(Guid.TryParse(h.Request.Headers["Idempotency-Key"], out var key) && key.Version == 7, 400, "invalid_operation_key", "A UUIDv7 Idempotency-Key is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var (_, member) = await access.RequireAsync(communityId, actor, ct);
        var current = await db.Announcements.Where(x => x.CommunityId == communityId).Select(x => x.Id).ToArrayAsync(ct);
        ApiFault.Require(current.Contains(announcementId), 404, "not_found", "Announcement is no longer available.");
        member.ReadAnnouncementIds = member.ReadAnnouncementIds.Where(current.Contains).Append(announcementId).Distinct().ToArray();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        // A naturally idempotent metadata PUT, without an audit/replay row per read.
        h.Response.Headers.CacheControl = "no-store";
        return Results.Json(new AnnouncementReadResult(announcementId, current.Count(x => !member.ReadAnnouncementIds.Contains(x))));
    }
}
