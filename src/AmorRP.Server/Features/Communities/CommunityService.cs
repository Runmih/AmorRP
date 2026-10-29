using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Communities;

public sealed partial class CommunityService(AmorDbContext db, SessionAccess sessions, CommunityAccess access, CommandStore commands,
    PageCursor pages, IOptions<ServicePolicyOptions> policy, TimeProvider time, AmorRP.Server.Features.Trading.TradeReservations trades)
{
    private async Task CapsAsync(Guid character, bool owning, CancellationToken ct)
    {
        var joined = await (from m in db.Memberships join g in db.Communities on m.CommunityId equals g.Id
            where m.CharacterId == character && m.Status == "active" && !g.Deleted select g.Id).CountAsync(ct);
        var owned = await db.Communities.CountAsync(x => x.OwnerCharacterId == character && !x.Deleted, ct);
        ApiFault.Require(joined < policy.Value.JoinedCommunities && (!owning || owned < policy.Value.OwnedCommunities), 409, "community_limit_reached", "Character community limit reached.");
    }
    public async Task<IResult> ListAsync(HttpContext http, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var rows = await (from m in db.Memberships join g in db.Communities on m.CommunityId equals g.Id
            where m.CharacterId == session.CharacterId && m.Status == "active" && !g.Deleted orderby g.Id select new { G = g, M = m }).ToArrayAsync(ct);
        var items = new List<Community>();
        foreach (var row in rows) items.Add(await access.ViewAsync(row.G, row.M, time.GetUtcNow(), ct));
        return Results.Json(new CommunityPage(items.ToArray(), null, string.Join('-', items.Select(x => $"{x.Id:N}:{x.Version}")), null));
    }
    private static void ValidateDescription(string description) => ApiFault.Require(AmorRP.Core.Items.PlainText.Error(description, CommunityEdit.MaximumDescriptionCharacters, multiline: true, required: false) == null, 422, "invalid_request", "Community description must contain at most 300 plain-text characters.");

    public async Task<IResult> CreateAsync(HttpContext http, CommunityCreate body, CancellationToken ct)
    {
        ApiFault.Text(body.Name, 80); ValidateDescription(body.Description); ApiFault.Text(body.CurrencyName, 80); ApiFault.Text(body.CurrencySymbol, 16, false);
        ValidatePolicy(body.WeeklyPotionPoints, body.WeeklyLetters);
        ApiFault.Require(body.WeeklyCookingPoints >= 0 && body.WeeklyCookingPoints <= policy.Value.MaxWeeklyPotionPoints, 422, "invalid_policy", "Cooking points exceed the supported range.");
        var session = await sessions.GetAsync(http, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId], ct);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null)
        {
            if (replay.CommunityId.HasValue) await access.RequireAsync(replay.CommunityId.Value, session.CharacterId, ct);
            return commands.Replay(replay);
        }
        await CapsAsync(session.CharacterId, true, ct);
        ApiFault.Require(await db.Communities.CountAsync(x => x.CreatedByCharacterId == session.CharacterId, ct) < policy.Value.MaxRetainedCreatedCommunitiesPerCharacter,
            409, "community_retention_limit_reached", $"Retained community creation limit ({policy.Value.MaxRetainedCreatedCommunitiesPerCharacter}) reached. Deleted and transferred communities count until eligible data is purged by an operator.");
        var community = new CommunityRow { Name = body.Name.Trim(), Description = body.Description, OwnerCharacterId = session.CharacterId,
            CreatedByCharacterId = session.CharacterId,
            CurrencyName = body.CurrencyName.Trim(), CurrencySymbol = body.CurrencySymbol, PolicyPeriod = CommunityAccess.Period(time.GetUtcNow()),
            CurrentPotionPoints = body.WeeklyPotionPoints, NextPotionPoints = body.WeeklyPotionPoints,
            CurrentCookingPoints = body.WeeklyCookingPoints, NextCookingPoints = body.WeeklyCookingPoints,
            CurrentLetters = body.WeeklyLetters, NextLetters = body.WeeklyLetters };
        var member = new MembershipRow { CommunityId = community.Id, CharacterId = session.CharacterId };
        db.Communities.Add(community); AmorRP.Server.Features.Catalog.CatalogService.Seed(db, community.Id); db.Memberships.Add(member);
        var op = commands.New(http, scope, body, session.CharacterId, community.Id, "community.create");
        http.Response.Headers.ETag = CommandStore.ETag("community", community.Id, community.Version);
        var result = await commands.SaveAsync(http, op, new CommandResult<Community>(op.Id, await access.ViewAsync(community, member, time.GetUtcNow(), ct)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> JoinAsync(HttpContext http, JoinCommunity body, CancellationToken ct)
    {
        ApiFault.Require(body.AcceptMemberVisibility && InvitationCode.IsValid(body.InvitationCode), 422, "invalid_request", "Accept community member visibility and provide a valid invitation.");
        var session = await sessions.GetAsync(http, ct);
        var hash = SecretVault.Hash(InvitationCode.Normalize(body.InvitationCode));
        var discovered = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
        ApiFault.Require(discovered != null, 404, "not_found", "Invitation not found.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "community:" + discovered!.CommunityId], ct);
        var community = await db.Communities.SingleOrDefaultAsync(x => x.Id == discovered!.CommunityId && !x.Deleted, ct);
        ApiFault.Require(community != null, 404, "not_found", "Invitation not found.");
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.CommunityId == community!.Id && x.CharacterId == session.CharacterId, ct);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null)
        { ApiFault.Require(member?.Status == "active", 404, "not_found", "Membership is no longer active."); return commands.Replay(replay); }
        ApiFault.Require(member?.Status != "blocked", 403, "membership_blocked", "Owner must restore your eligibility before rejoining.");
        ApiFault.Require(member?.Status != "active", 409, "already_joined", "Character is already a member.");
        var invite = await db.Invitations.SingleAsync(x => x.Id == discovered!.Id, ct);
        ApiFault.Require(!invite.Revoked && invite.ExpiresAt > time.GetUtcNow() && invite.Used < invite.MaxUses, 409, "invitation_unavailable", "This invitation is no longer available. Ask the community owner for a new code.");
        await CapsAsync(session.CharacterId, false, ct);
        if (member == null) { member = new MembershipRow { CommunityId = community!.Id, CharacterId = session.CharacterId }; db.Memberships.Add(member); }
        else { member.Status = "active"; member.Capabilities = []; member.Version++; }
        invite.Used++; community!.Version++;
        var op = commands.New(http, scope, body, session.CharacterId, community.Id, "community.join");
        var result = await commands.SaveAsync(http, op, new CommandResult<Community>(op.Id, await access.ViewAsync(community, member, time.GetUtcNow(), ct)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> ReadAsync(HttpContext http, Guid communityId, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var (community, member) = await access.RequireAsync(communityId, session.CharacterId, ct);
        object response;
        switch (kind)
        {
            case "policies": response = CommunityAccess.Policies(community, time.GetUtcNow()); http.Response.Headers.ETag = CommandStore.ETag("policy", community.Id, community.PolicyVersion); break;
            case "currency": response = CommunityAccess.Currency(community); http.Response.Headers.ETag = CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion); break;
            case "balances": response = new BalancePage([CommunityAccess.Balance(community, member)], null, member.BalanceVersion.ToString(), null); break;
            default: response = await access.ViewAsync(community, member, time.GetUtcNow(), ct); http.Response.Headers.ETag = CommandStore.ETag("community", community.Id, community.Version); break;
        }
        return Results.Json(response);
    }
    public async Task<IResult> EditAsync(HttpContext http, Guid communityId, object? body, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "community:" + communityId], ct);
        var (community, member) = await access.RequireAsync(communityId, session.CharacterId, ct);
        if (kind == "leave") ApiFault.Require(community.OwnerCharacterId != session.CharacterId, 409, "owner_cannot_leave", "Transfer ownership or delete the community first.");
        else if (kind is "edit" or "currency") CommunityAccess.Action(community, member, "community.moderate");
        else CommunityAccess.Owner(community, session.CharacterId);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(http, scope, body, session.CharacterId, communityId, "community." + kind);
        object? payload = null;
        switch (body)
        {
            case CommunityEdit edit:
                ApiFault.Require(community.OwnerCharacterId == session.CharacterId || string.Equals(edit.Name?.Trim(), community.Name, StringComparison.Ordinal), 403, "owner_required", "Only the owner may rename the community.");
                CommandStore.Match(http, CommandStore.ETag("community", community.Id, community.Version));
                ApiFault.Text(edit.Name, 80); ValidateDescription(edit.Description);
                community.Name = edit.Name!.Trim(); community.Description = edit.Description; community.Version++;
                payload = await access.ViewAsync(community, member, time.GetUtcNow(), ct); http.Response.Headers.ETag = CommandStore.ETag("community", community.Id, community.Version); break;
            case PolicyEdit edit:
                CommandStore.Match(http, CommandStore.ETag("policy", community.Id, community.PolicyVersion)); ValidatePolicy(edit.PotionPoints, edit.Letters);
                ApiFault.Require(edit.CookingPoints >= 0 && edit.CookingPoints <= policy.Value.MaxWeeklyPotionPoints, 422, "invalid_policy", "Cooking points exceed the supported range.");
                CommunityAccess.PromotePolicy(community, time.GetUtcNow()); community.NextCookingPoints = edit.CookingPoints; community.NextPotionPoints = edit.PotionPoints; community.NextLetters = edit.Letters; community.PolicyVersion++;
                payload = CommunityAccess.Policies(community, time.GetUtcNow()); http.Response.Headers.ETag = CommandStore.ETag("policy", community.Id, community.PolicyVersion); break;
            case CurrencyEdit edit:
                ApiFault.Require(community.OwnerCharacterId == session.CharacterId || edit.Symbol == community.CurrencySymbol, 403, "owner_required", "General Moderation permits changing the currency name only.");
                CommandStore.Match(http, CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion));
                ApiFault.Text(edit.Name, 80); ApiFault.Text(edit.Symbol, 16, false); community.CurrencyName = edit.Name.Trim(); community.CurrencySymbol = edit.Symbol; community.CurrencyVersion++;
                payload = CommunityAccess.Currency(community); http.Response.Headers.ETag = CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion); break;
            case CommunityDeletion deletion:
                await trades.CancelForAsync(communityId, null, "Community deleted.", ct);
                CommandStore.Match(http, CommandStore.ETag("community", community.Id, community.Version));
                ApiFault.Require(deletion.ConfirmName == community.Name, 422, "confirmation_required", "Confirm the exact community name.");
                community.Deleted = true; community.DeletedAt = time.GetUtcNow(); community.Version++;
                foreach (var invite in await db.Invitations.Where(x => x.CommunityId == communityId && !x.Revoked).ToArrayAsync(ct)) invite.Revoked = true;
                foreach (var transfer in await db.OwnershipTransfers.Where(x => x.CommunityId == communityId && x.Status == "proposed").ToArrayAsync(ct)) transfer.Status = "cancelled";
                break;
            default:
                ApiFault.Require(kind == "leave", 400, "invalid_request", "Unknown community command.");
                member.Status = "dormant"; member.Capabilities = []; member.Version++; community.Version++;
                await EndMemberAccessAsync(communityId, member.CharacterId, ct); break;
        }
        var response = payload == null ? (object)new Receipt(op.Id) : new CommandResult<object>(op.Id, payload);
        var result = await commands.SaveAsync(http, op, response, ct); await tx.CommitAsync(ct); return result;
    }
    private void ValidatePolicy(int points, int letters) => ApiFault.Require(points >= 0 && points <= policy.Value.MaxWeeklyPotionPoints && letters >= 0 && letters <= policy.Value.MaxWeeklyLetters,
        422, "invalid_request", "Weekly limits exceed supported bounds.");
    private async Task EndMemberAccessAsync(Guid community, Guid member, CancellationToken ct)
    {
        foreach (var groupMember in await db.GroupMemberships.Where(x => x.CommunityId == community && x.CharacterId == member).ToArrayAsync(ct))
        { groupMember.Capabilities = []; groupMember.PermissionVersion = Guid.CreateVersion7(); }
        await trades.CancelForAsync(community, member, "Membership ended or trading was restricted.", ct);
        foreach (var transfer in await db.OwnershipTransfers.Where(x => x.CommunityId == community && x.ToCharacterId == member && x.Status == "proposed").ToArrayAsync(ct)) transfer.Status = "cancelled";
    }

}
