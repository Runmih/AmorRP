using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Groups;

public sealed partial class GroupService
{
    public async Task<IResult> MembersAsync(HttpContext http, Guid groupId, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); var (group, _) = await access.RequireAsync(groupId, session.CharacterId, ct);
        var dormant = http.Request.Query["includeDormant"] == "true";
        if (dormant) GroupAccess.Owner(group, session.CharacterId);
        var scope = $"members:{groupId}:{session.CharacterId}:{dormant}"; var (limit, after) = pages.Read(http, scope);
        var query = db.Memberships.Where(x => x.GroupId == groupId && (dormant || x.Status == "active"));
        if (after.HasValue) query = query.Where(x => x.CharacterId.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.CharacterId).Take(limit + 1).ToArrayAsync(ct);
        var items = new List<Member>();
        foreach (var row in rows.Take(limit))
        {
            var character = await db.Characters.SingleAsync(x => x.Id == row.CharacterId, ct);
            items.Add(new(SessionAccess.View(character), row.Status, group.OwnerCharacterId == row.CharacterId,
                group.OwnerCharacterId == row.CharacterId ? GroupAccess.GrantableActions : row.Capabilities, row.TradingRestricted, row.Version, GroupAccess.MemberETag(row), row.Description));
        }
        return Results.Json(new MemberPage(items.ToArray(), rows.Length > limit ? pages.Write(rows[limit-1].CharacterId, scope) : null, group.Version.ToString(), null));
    }
    public async Task<IResult> MemberCommandAsync(HttpContext http, Guid groupId, Guid characterId, object body, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "character:" + characterId, "group:" + groupId], ct);
        var (group, actorMember) = await access.RequireAsync(groupId, session.CharacterId, ct);
        if (kind == "capabilities") GroupAccess.Action(group, actorMember, "members.gameplay.manage");
        else GroupAccess.Owner(group, session.CharacterId);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        ApiFault.Require(characterId != group.OwnerCharacterId, 409, "owner_protected", "Owner authority cannot be edited as a member grant.");
        var target = await access.TargetAsync(groupId, characterId, kind != "restore", ct);
        CommandStore.Match(http, GroupAccess.MemberETag(target));
        var op = commands.New(http, scope, body, session.CharacterId, groupId, "member." + kind); op.SubjectCharacterId = characterId;
        switch (body)
        {
            case MemberCapabilities grant:
                ApiFault.Require(grant.Capabilities != null && grant.Capabilities.Length <= GroupAccess.GrantableActions.Length
                    && grant.Capabilities.Distinct().Count() == grant.Capabilities.Length && grant.Capabilities.All(GroupAccess.GrantableActions.Contains),
                    422, "invalid_request", "Unknown or repeated capability.");
                if (group.OwnerCharacterId != session.CharacterId)
                {
                    var protectedBefore = target.Capabilities.Where(x => x != "items.potion.create").Order();
                    var protectedAfter = grant.Capabilities!.Where(x => x != "items.potion.create").Order();
                    ApiFault.Require(protectedBefore.SequenceEqual(protectedAfter), 403, "owner_required", "Only the owner may change moderator permissions.");
                }
                target.Capabilities = grant.Capabilities!.Order().ToArray();
                if (grant.TradingAllowed.HasValue)
                {
                    target.TradingRestricted = !grant.TradingAllowed.Value;
                    if (target.TradingRestricted) await trades.CancelForAsync(groupId, characterId, "Trading permission disabled.", ct);
                }
                op.Summary = "Member permissions updated; trading " + (target.TradingRestricted ? "disabled." : "enabled.");
                break;
            case MemberRestriction restriction:
                ApiFault.Text(restriction.Reason, 500, false); target.TradingRestricted = restriction.Restricted; op.Reason = restriction.Reason;
                if (restriction.Restricted) await trades.CancelForAsync(groupId, characterId, "Trading permission disabled.", ct); break;
            case MemberRemoval removal:
                ApiFault.Text(removal.Reason, 500); op.Reason = removal.Reason;
                if (kind == "restore") { ApiFault.Require(target.Status == "blocked", 409, "member_not_blocked", "Only blocked members can be restored."); target.Status = "dormant"; }
                else { target.Status = "blocked"; target.Capabilities = []; await CancelTransfersForAsync(groupId, characterId, ct); }
                break;
        }
        target.Version++; group.Version++;
        var dto = new Member(SessionAccess.View(await db.Characters.SingleAsync(x => x.Id == characterId, ct)), target.Status, false,
            target.Capabilities, target.TradingRestricted, target.Version, GroupAccess.MemberETag(target), target.Description);
        http.Response.Headers.ETag = dto.Etag;
        var result = await commands.SaveAsync(http, op, new CommandResult<Member>(op.Id, dto), ct); await tx.CommitAsync(ct); return result;
    }

    public async Task<IResult> MemberAsync(HttpContext h, Guid groupId, Guid characterId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (group, _) = await access.RequireAsync(groupId, actor, ct);
        var target = await access.TargetAsync(groupId, characterId, actor != group.OwnerCharacterId, ct);
        h.Response.Headers.ETag = GroupAccess.MemberETag(target);
        return Results.Json(await MemberViewAsync(group, target, ct));
    }
    private async Task<Member> MemberViewAsync(GroupRow g, MembershipRow m, CancellationToken ct) => new(
        SessionAccess.View(await db.Characters.SingleAsync(x => x.Id == m.CharacterId, ct)), m.Status,
        m.CharacterId == g.OwnerCharacterId, m.CharacterId == g.OwnerCharacterId ? GroupAccess.GrantableActions : m.Capabilities,
        m.TradingRestricted, m.Version, GroupAccess.MemberETag(m), m.Description);
    public async Task<IResult> MembershipAsync(HttpContext h, Guid groupId, Guid characterId, MemberRemoval body, string kind, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "character:" + characterId, "group:" + groupId], ct);
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var (group, _) = await access.RequireAsync(groupId, actor, ct);
        if (actor != characterId || kind != "remove") GroupAccess.Owner(group, actor);
        ApiFault.Require(characterId != group.OwnerCharacterId, 409, "owner_protected", "Transfer ownership or delete the group first.");
        var target = await access.TargetAsync(groupId, characterId, false, ct);
        CommandStore.Match(h, GroupAccess.MemberETag(target));
        ApiFault.Text(body.Reason, 500, actor != characterId || kind != "remove");
        if (kind == "unblock")
        {
            ApiFault.Require(target.Status == "blocked", 409, "member_not_blocked", "Member is not blocked."); target.Status = "dormant";
        }
        else
        {
            ApiFault.Require(kind == "block" || target.Status == "active", 409, "membership_inactive", "Membership is already inactive.");
            target.Status = kind == "block" ? "blocked" : "dormant"; target.Capabilities = [];
            await CancelTransfersForAsync(groupId, characterId, ct);
        }
        target.Version++; group.Version++;
        var op = commands.New(h, scope, body, actor, groupId, "member." + kind); op.SubjectCharacterId = characterId;
        op.Reason = body.Reason; op.Summary = kind == "remove" ? "Membership ended; invited rejoining is allowed." : kind == "block" ? "Member blocked from rejoining." : "Block lifted; membership remains inactive.";
        h.Response.Headers.ETag = GroupAccess.MemberETag(target);
        var result = await commands.SaveAsync(h, op, new CommandResult<Member>(op.Id, await MemberViewAsync(group, target, ct)), ct);
        await tx.CommitAsync(ct); return result;
    }
}
