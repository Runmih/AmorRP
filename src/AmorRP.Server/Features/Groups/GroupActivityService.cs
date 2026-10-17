using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Groups;

public sealed class GroupActivityService(AmorDbContext db, SessionAccess sessions, CommunityAccess communities, CommandStore commands, TimeProvider time, PageCursor pages)
{
    public static void Owner(GroupRow row, Guid actor) => ApiFault.Require(row.OwnerCharacterId == actor, 403, "group_owner_required", "Only this group's owner can change its contents.");
    private async Task<(Guid Actor, GroupRow Group)> RequireAsync(HttpContext h, Guid communityId, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await communities.RequireAsync(communityId, actor, ct);
        var group = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == groupId && !x.Deleted, ct);
        ApiFault.Require(group != null, 404, "not_found", "Group is no longer available."); return (actor, group!);
    }
    private static GroupEvent Event(GroupEventRow row, string name) => new(row.Id, row.CommunityId, row.GroupId, name, row.Title, row.Description, row.Location, row.StartsAt, row.EndsAt, row.Version);
    private static void Text(string value, int maximum, bool required = true)
    {
        var error = PlainText.Error(value, maximum, multiline: true, required: required);
        ApiFault.Require(error == null, 422, "invalid_text", error ?? "Check the text.");
    }
    public async Task<IResult> HubAsync(HttpContext h, Guid communityId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId; await communities.RequireAsync(communityId, actor, ct);
        var notices = await (from n in db.GroupNotices.AsNoTracking() join g in db.Groups on new { n.CommunityId, Id = n.GroupId } equals new { g.CommunityId, g.Id }
            where n.CommunityId == communityId && !g.Deleted orderby g.Name select new GroupNotice(n.GroupId, g.Name, n.Title, n.Message, n.Reward, n.Version)).ToArrayAsync(ct);
        var events = await (from e in db.GroupEvents.AsNoTracking() join g in db.Groups on new { e.CommunityId, Id = e.GroupId } equals new { g.CommunityId, g.Id }
            where e.CommunityId == communityId && !g.Deleted && e.EndsAt > time.GetUtcNow() orderby e.StartsAt select new { Row = e, g.Name }).ToArrayAsync(ct);
        return Results.Json(new GroupHub(notices, events.Select(x => Event(x.Row, x.Name)).ToArray()));
    }
    public async Task<IResult> FavoritesAsync(HttpContext h, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var rows = await (from f in db.GroupFavorites.AsNoTracking()
            join m in db.Memberships on new { f.CommunityId, f.CharacterId } equals new { m.CommunityId, m.CharacterId }
            join c in db.Communities on f.CommunityId equals c.Id
            join g in db.Groups on new { f.CommunityId, Id = f.GroupId } equals new { g.CommunityId, g.Id }
            join e in db.GroupEvents on new { g.CommunityId, GroupId = g.Id } equals new { e.CommunityId, e.GroupId }
            where f.CharacterId == actor && m.Status == "active" && !c.Deleted && !g.Deleted && e.EndsAt > time.GetUtcNow()
            orderby e.StartsAt select new { Row = e, g.Name }).Take(200).ToArrayAsync(ct);
        return Results.Json(new FavoriteEvents(rows.Select(x => Event(x.Row, x.Name)).ToArray()));
    }
    public async Task<IResult> PrivateAsync(HttpContext h, Guid communityId, Guid groupId, CancellationToken ct)
    {
        var (actor, _) = await RequireAsync(h, communityId, groupId, ct);
        ApiFault.Require(await db.GroupMemberships.AnyAsync(x => x.CommunityId == communityId && x.GroupId == groupId && x.CharacterId == actor, ct), 403, "group_membership_required", "Join this group to view its members and announcements.");
        var scope = $"group-members:{communityId}:{groupId}:{actor}"; var (limit, after) = pages.Read(h, scope);
        var members = await (from gm in db.GroupMemberships.AsNoTracking()
            join m in db.Memberships on new { gm.CommunityId, gm.CharacterId } equals new { m.CommunityId, m.CharacterId }
            join c in db.Characters on gm.CharacterId equals c.Id
            join g in db.Groups on new { gm.CommunityId, Id = gm.GroupId } equals new { g.CommunityId, g.Id }
            where gm.CommunityId == communityId && gm.GroupId == groupId && m.Status == "active" && (!after.HasValue || c.Id.CompareTo(after.Value) > 0)
            orderby c.Id select new GroupMember(c.Id, c.DisplayName, c.HomeWorldName, g.OwnerCharacterId == c.Id)).Take(limit + 1).ToArrayAsync(ct);
        var posts = await db.GroupAnnouncements.AsNoTracking().Where(x => x.CommunityId == communityId && x.GroupId == groupId).OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id).Take(5)
            .Select(x => new GroupAnnouncement(x.Id, x.Title, x.Message, x.PublishedAt)).ToArrayAsync(ct);
        h.Response.Headers.CacheControl = "no-store";
        return Results.Json(new GroupPrivate(members.Take(limit).ToArray(), posts, members.Length > limit ? pages.Write(members[limit - 1].CharacterId, scope) : null));
    }
    public async Task<IResult> CommandAsync(HttpContext h, Guid communityId, Guid groupId, string kind, object? body, Guid? eventId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var (_, group) = await RequireAsync(h, communityId, groupId, ct);
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        if (kind is "notice" or "event" or "announcement") Owner(group, actor);
        switch (kind)
        {
            case "join":
                ApiFault.Require(group.JoinMode != "closed", 403, "group_closed", "This group is closed to new members.");
                if (!await db.GroupMemberships.AnyAsync(x => x.CommunityId == communityId && x.GroupId == groupId && x.CharacterId == actor, ct))
                {
                    var supplied = ((GroupJoin)body!).Password;
                    ApiFault.Require(supplied == null || supplied.Length <= 64, 422, "invalid_password", "Password is too long.");
                    ApiFault.Require(group.JoinMode != "password" || (group.PasswordHash != null && supplied != null && new PasswordHasher<GroupRow>().VerifyHashedPassword(group, group.PasswordHash, supplied) != PasswordVerificationResult.Failed), 403, "group_password_invalid", "That group password is incorrect.");
                    db.GroupMemberships.Add(new() { CommunityId = communityId, GroupId = groupId, CharacterId = actor });
                }
                break;
            case "leave":
                ApiFault.Require(group.OwnerCharacterId != actor, 409, "group_owner_leave", "Delete your group before leaving it.");
                await db.GroupMemberships.Where(x => x.CommunityId == communityId && x.GroupId == groupId && x.CharacterId == actor).ExecuteDeleteAsync(ct); break;
            case "favorite":
                var favorite = await db.GroupFavorites.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.GroupId == groupId && x.CharacterId == actor, ct);
                if (h.Request.Method == "DELETE") { if (favorite != null) db.GroupFavorites.Remove(favorite); }
                else if (favorite == null) db.GroupFavorites.Add(new() { CommunityId = communityId, GroupId = groupId, CharacterId = actor });
                break;
            case "notice":
                var notice = await db.GroupNotices.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.GroupId == groupId, ct);
                if (body == null) { if (notice != null) { CommandStore.Match(h, CommandStore.ETag("group-notice", groupId, notice.Version)); db.GroupNotices.Remove(notice); } break; }
                var n = (NoticeWrite)body; Text(n.Title, 40); Text(n.Message, 300); Text(n.Reward, 100, false);
                if (notice == null) { notice = new() { CommunityId = communityId, GroupId = groupId }; db.GroupNotices.Add(notice); }
                else { CommandStore.Match(h, CommandStore.ETag("group-notice", groupId, notice.Version)); notice.Version++; }
                notice.Title = n.Title.Trim(); notice.Message = n.Message; notice.Reward = n.Reward ?? ""; break;
            case "event":
                var row = eventId.HasValue ? await db.GroupEvents.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.GroupId == groupId && x.Id == eventId, ct) : null;
                if (eventId.HasValue) { ApiFault.Require(row != null, 404, "not_found", "Event is no longer available."); CommandStore.Match(h, CommandStore.ETag("group-event", row!.Id, row.Version)); }
                if (body == null) { ApiFault.Require(row != null, 404, "not_found", "Choose an event."); db.GroupEvents.Remove(row!); break; }
                var e = (EventWrite)body; Text(e.Title, 40); Text(e.Description, 100); Text(e.Location, 100);
                ApiFault.Require(e.EndsAt > e.StartsAt && e.EndsAt > time.GetUtcNow() && e.StartsAt < time.GetUtcNow().AddYears(2), 422, "invalid_event_range", "Enter a future event with an end after its start, within the next two years.");
                if (row == null)
                {
                    await db.GroupEvents.Where(x => x.CommunityId == communityId && x.GroupId == groupId && x.EndsAt <= time.GetUtcNow()).ExecuteDeleteAsync(ct);
                    ApiFault.Require(await db.GroupEvents.CountAsync(x => x.CommunityId == communityId && x.GroupId == groupId, ct) < 20, 409, "group_events_full", "A group can schedule up to 20 upcoming events.");
                    row = new() { CommunityId = communityId, GroupId = groupId }; db.GroupEvents.Add(row);
                }
                else row.Version++;
                row.Title = e.Title.Trim(); row.Description = e.Description ?? ""; row.Location = e.Location; row.StartsAt = e.StartsAt.ToUniversalTime(); row.EndsAt = e.EndsAt.ToUniversalTime(); break;
            case "announcement":
                var a = (AnnouncementWrite)body!; Text(a.Title, AmorRP.Contracts.Announcements.AnnouncementCreate.MaximumTitleCharacters); Text(a.Message, AmorRP.Contracts.Announcements.AnnouncementCreate.MaximumBodyCharacters);
                db.GroupAnnouncements.Add(new() { CommunityId = communityId, GroupId = groupId, Title = a.Title.Trim(), Message = a.Message, PublishedAt = time.GetUtcNow() });
                var remove = await db.GroupAnnouncements.Where(x => x.CommunityId == communityId && x.GroupId == groupId).OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id).Skip(4).ToArrayAsync(ct);
                db.GroupAnnouncements.RemoveRange(remove); break;
            default: throw new ApiFault(400, "invalid_request", "Unknown group action.");
        }
        var op = commands.New(h, scope, body, actor, communityId, "group." + kind);
        op.Summary = "Group " + kind + " updated.";
        var result = await commands.SaveAsync(h, op, new Receipt(op.Id), ct); await tx.CommitAsync(ct); return result;
    }
}
