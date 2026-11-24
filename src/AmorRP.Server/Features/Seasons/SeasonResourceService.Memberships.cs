using AmorRP.Contracts.Auth;
using AmorRP.Core.Items;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Seasons;

public sealed partial class SeasonResourceService
{
    public async Task<IResult> MembersAsync(HttpContext http, Guid communityId, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); var (community, _) = await access.RequireAsync(communityId, session.CharacterId, ct);
        var search = http.Request.Query["search"].ToString().Trim();
        ApiFault.Require(search.Length <= 200, 422, "invalid_request", "Member search must be 200 characters or fewer.");
        var scope = $"members:{communityId}:{session.CharacterId}:{SecretVault.Hash(search)}"; var (limit, after) = pages.Read(http, scope);
        var query = db.Memberships.Where(x => x.CommunityId == communityId && x.Status == "active");
        if (search.Length > 0)
        {
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var matching = db.Characters.Where(x => EF.Functions.ILike(x.DisplayName + " " + x.HomeWorldName, pattern, "\\")).Select(x => x.Id);
            query = query.Where(x => matching.Contains(x.CharacterId));
        }
        if (after.HasValue) query = query.Where(x => x.CharacterId.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.CharacterId).Take(limit + 1).ToArrayAsync(ct);
        var characters = await db.Characters.Where(x => rows.Select(m => m.CharacterId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var items = new List<Member>();
        foreach (var row in rows.Take(limit))
        {
            var character = characters[row.CharacterId];
            items.Add(new(SessionAccess.View(character), row.Status, community.OwnerCharacterId == row.CharacterId,
                community.OwnerCharacterId == row.CharacterId ? SeasonAccess.GrantableActions : row.Capabilities, row.TradingRestricted, row.Version, SeasonAccess.MemberETag(row), row.Description, row.CombatRestricted));
        }
        return Results.Json(new MemberPage(items.ToArray(), rows.Length > limit ? pages.Write(rows[limit-1].CharacterId, scope) : null, community.Version.ToString(), null));
    }
    public async Task<IResult> MemberCommandAsync(HttpContext http, Guid communityId, Guid characterId, object body, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "character:" + characterId, "season:" + communityId], ct);
        var (community, actorMember) = await access.RequireAsync(communityId, session.CharacterId, ct);
        if (kind == "capabilities") SeasonAccess.Action(community, actorMember, "members.gameplay.manage");
        else SeasonAccess.Owner(community, session.CharacterId);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        ApiFault.Require(characterId != community.OwnerCharacterId, 409, "owner_protected", "Owner authority cannot be edited as a member grant.");
        var target = await access.TargetAsync(communityId, characterId, true, ct);
        CommandStore.Match(http, SeasonAccess.MemberETag(target));
        var op = commands.New(http, scope, body, session.CharacterId, communityId, "member." + kind); op.SubjectCharacterId = characterId;
        switch (body)
        {
            case MemberCapabilities grant:
                ApiFault.Require(grant.Capabilities != null && grant.Capabilities.Length <= SeasonAccess.GrantableActions.Length
                    && grant.Capabilities.Distinct().Count() == grant.Capabilities.Length && grant.Capabilities.All(SeasonAccess.GrantableActions.Contains),
                    422, "invalid_request", "Unknown or repeated capability.");
                if (community.OwnerCharacterId != session.CharacterId)
                {
                    var protectedBefore = target.Capabilities.Where(x => x is not ("items.potion.create" or "items.cooking.create" or CraftingTypes.GatherPermission or CraftingTypes.BlacksmithPermission)).Order();
                    var protectedAfter = grant.Capabilities!.Where(x => x is not ("items.potion.create" or "items.cooking.create" or CraftingTypes.GatherPermission or CraftingTypes.BlacksmithPermission)).Order();
                    ApiFault.Require(protectedBefore.SequenceEqual(protectedAfter), 403, "owner_required", "Only the owner may change moderator permissions.");
                }
                target.Capabilities = grant.Capabilities!.Order().ToArray();
                if (grant.CombatAllowed is { } combatAllowed) target.CombatRestricted = !combatAllowed;
                if (grant.TradingAllowed.HasValue)
                {
                    target.TradingRestricted = !grant.TradingAllowed.Value;
                    if (target.TradingRestricted) await trades.CancelForAsync(communityId, characterId, "Trading permission disabled.", ct);
                }
                op.Summary = "Member permissions updated; trading " + (target.TradingRestricted ? "disabled." : "enabled.");
                break;
            case MemberRestriction restriction:
                ApiFault.Text(restriction.Reason, 500, false); target.TradingRestricted = restriction.Restricted; op.Reason = restriction.Reason;
                if (restriction.Restricted) await trades.CancelForAsync(communityId, characterId, "Trading permission disabled.", ct); break;

        }
        target.Version++; community.Version++;
        var dto = new Member(SessionAccess.View(await db.Characters.SingleAsync(x => x.Id == characterId, ct)), target.Status, false,
            target.Capabilities, target.TradingRestricted, target.Version, SeasonAccess.MemberETag(target), target.Description, target.CombatRestricted);
        http.Response.Headers.ETag = dto.Etag;
        var result = await commands.SaveAsync(http, op, new CommandResult<Member>(op.Id, dto), ct); await tx.CommitAsync(ct); return result;
    }

    public async Task<IResult> MemberAsync(HttpContext h, Guid communityId, Guid characterId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (community, _) = await access.RequireAsync(communityId, actor, ct);
        var target = await access.TargetAsync(communityId, characterId, actor != community.OwnerCharacterId, ct);
        h.Response.Headers.ETag = SeasonAccess.MemberETag(target);
        return Results.Json(await MemberViewAsync(community, target, ct));
    }
    private async Task<Member> MemberViewAsync(CommunityRow g, MembershipRow m, CancellationToken ct) => new(
        SessionAccess.View(await db.Characters.SingleAsync(x => x.Id == m.CharacterId, ct)), m.Status,
        m.CharacterId == g.OwnerCharacterId, m.CharacterId == g.OwnerCharacterId ? SeasonAccess.GrantableActions : m.Capabilities,
        m.TradingRestricted, m.Version, SeasonAccess.MemberETag(m), m.Description, m.CombatRestricted);
}
