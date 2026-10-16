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

public sealed partial class GroupService(AmorDbContext db, SessionAccess sessions, GroupAccess access, CommandStore commands,
    PageCursor pages, IOptions<ServicePolicyOptions> policy, TimeProvider time, AmorRP.Server.Features.Trading.TradeReservations trades)
{
    private async Task CapsAsync(Guid character, bool owning, CancellationToken ct)
    {
        var joined = await (from m in db.Memberships join g in db.Groups on m.GroupId equals g.Id
            where m.CharacterId == character && m.Status == "active" && !g.Deleted select g.Id).CountAsync(ct);
        var owned = await db.Groups.CountAsync(x => x.OwnerCharacterId == character && !x.Deleted, ct);
        ApiFault.Require(joined < policy.Value.JoinedGroups && (!owning || owned < policy.Value.OwnedGroups), 409, "group_limit_reached", "Character group limit reached.");
    }
    public async Task<IResult> ListAsync(HttpContext http, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var rows = await (from m in db.Memberships join g in db.Groups on m.GroupId equals g.Id
            where m.CharacterId == session.CharacterId && m.Status == "active" && !g.Deleted orderby g.Id select new { G = g, M = m }).ToArrayAsync(ct);
        var items = new List<Group>();
        foreach (var row in rows) items.Add(await access.ViewAsync(row.G, row.M, time.GetUtcNow(), ct));
        return Results.Json(new GroupPage(items.ToArray(), null, string.Join('-', items.Select(x => $"{x.Id:N}:{x.Version}")), null));
    }
    private static void ValidateDescription(string description) => ApiFault.Require(AmorRP.Core.Items.PlainText.Error(description, GroupEdit.MaximumDescriptionCharacters, multiline: true, required: false) == null, 422, "invalid_request", "Group description must contain at most 300 plain-text characters.");

    public async Task<IResult> CreateAsync(HttpContext http, GroupCreate body, CancellationToken ct)
    {
        ApiFault.Text(body.Name, 80); ValidateDescription(body.Description); ApiFault.Text(body.CurrencyName, 80); ApiFault.Text(body.CurrencySymbol, 16, false);
        ValidatePolicy(body.WeeklyPotionPoints, body.WeeklyLetters);
        var session = await sessions.GetAsync(http, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId], ct);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null)
        {
            if (replay.GroupId.HasValue) await access.RequireAsync(replay.GroupId.Value, session.CharacterId, ct);
            return commands.Replay(replay);
        }
        await CapsAsync(session.CharacterId, true, ct);
        ApiFault.Require(await db.Groups.CountAsync(x => x.CreatedByCharacterId == session.CharacterId, ct) < policy.Value.MaxRetainedCreatedGroupsPerCharacter,
            409, "group_retention_limit_reached", $"Retained group creation limit ({policy.Value.MaxRetainedCreatedGroupsPerCharacter}) reached. Deleted and transferred groups count until eligible data is purged by an operator.");
        var group = new GroupRow { Name = body.Name.Trim(), Description = body.Description, OwnerCharacterId = session.CharacterId,
            CreatedByCharacterId = session.CharacterId,
            CurrencyName = body.CurrencyName.Trim(), CurrencySymbol = body.CurrencySymbol, PolicyPeriod = GroupAccess.Period(time.GetUtcNow()),
            CurrentPotionPoints = body.WeeklyPotionPoints, NextPotionPoints = body.WeeklyPotionPoints,
            CurrentLetters = body.WeeklyLetters, NextLetters = body.WeeklyLetters };
        var member = new MembershipRow { GroupId = group.Id, CharacterId = session.CharacterId };
        db.Groups.Add(group); AmorRP.Server.Features.Catalog.CatalogService.Seed(db, group.Id); db.Memberships.Add(member);
        var op = commands.New(http, scope, body, session.CharacterId, group.Id, "group.create");
        http.Response.Headers.ETag = CommandStore.ETag("group", group.Id, group.Version);
        var result = await commands.SaveAsync(http, op, new CommandResult<Group>(op.Id, await access.ViewAsync(group, member, time.GetUtcNow(), ct)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> JoinAsync(HttpContext http, JoinGroup body, CancellationToken ct)
    {
        ApiFault.Require(body.AcceptMemberVisibility && SecretVault.IsCredential(body.InvitationCode), 422, "invalid_request", "Accept group member visibility and provide a valid invitation.");
        var session = await sessions.GetAsync(http, ct);
        var hash = SecretVault.Hash(body.InvitationCode);
        var discovered = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
        ApiFault.Require(discovered != null, 404, "not_found", "Invitation not found.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "group:" + discovered!.GroupId], ct);
        var group = await db.Groups.SingleOrDefaultAsync(x => x.Id == discovered!.GroupId && !x.Deleted, ct);
        ApiFault.Require(group != null, 404, "not_found", "Invitation not found.");
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.GroupId == group!.Id && x.CharacterId == session.CharacterId, ct);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null)
        { ApiFault.Require(member?.Status == "active", 404, "not_found", "Membership is no longer active."); return commands.Replay(replay); }
        ApiFault.Require(member?.Status != "blocked", 403, "membership_blocked", "Owner must restore your eligibility before rejoining.");
        ApiFault.Require(member?.Status != "active", 409, "already_joined", "Character is already a member.");
        var invite = await db.Invitations.SingleAsync(x => x.Id == discovered!.Id, ct);
        ApiFault.Require(!invite.Revoked && invite.ExpiresAt > time.GetUtcNow() && invite.Used < invite.MaxUses, 409, "invitation_unavailable", "Invitation expired, revoked or fully used.");
        await CapsAsync(session.CharacterId, false, ct);
        if (member == null) { member = new MembershipRow { GroupId = group!.Id, CharacterId = session.CharacterId }; db.Memberships.Add(member); }
        else { member.Status = "active"; member.Capabilities = []; member.Version++; }
        invite.Used++; group!.Version++;
        var op = commands.New(http, scope, body, session.CharacterId, group.Id, "group.join");
        var result = await commands.SaveAsync(http, op, new CommandResult<Group>(op.Id, await access.ViewAsync(group, member, time.GetUtcNow(), ct)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> ReadAsync(HttpContext http, Guid groupId, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var (group, member) = await access.RequireAsync(groupId, session.CharacterId, ct);
        object response;
        switch (kind)
        {
            case "policies": response = GroupAccess.Policies(group, time.GetUtcNow()); http.Response.Headers.ETag = CommandStore.ETag("policy", group.Id, group.PolicyVersion); break;
            case "currency": response = GroupAccess.Currency(group); http.Response.Headers.ETag = CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion); break;
            case "balances": response = new BalancePage([GroupAccess.Balance(group, member)], null, member.BalanceVersion.ToString(), null); break;
            default: response = await access.ViewAsync(group, member, time.GetUtcNow(), ct); http.Response.Headers.ETag = CommandStore.ETag("group", group.Id, group.Version); break;
        }
        return Results.Json(response);
    }
    public async Task<IResult> EditAsync(HttpContext http, Guid groupId, object? body, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "group:" + groupId], ct);
        var (group, member) = await access.RequireAsync(groupId, session.CharacterId, ct);
        if (kind == "leave") ApiFault.Require(group.OwnerCharacterId != session.CharacterId, 409, "owner_cannot_leave", "Transfer ownership or delete the group first.");
        else if (kind is "edit" or "currency") GroupAccess.Action(group, member, "group.moderate");
        else GroupAccess.Owner(group, session.CharacterId);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(http, scope, body, session.CharacterId, groupId, "group." + kind);
        object? payload = null;
        switch (body)
        {
            case GroupEdit edit:
                ApiFault.Require(group.OwnerCharacterId == session.CharacterId || string.Equals(edit.Name?.Trim(), group.Name, StringComparison.Ordinal), 403, "owner_required", "Only the owner may rename the group.");
                CommandStore.Match(http, CommandStore.ETag("group", group.Id, group.Version));
                ApiFault.Text(edit.Name, 80); ValidateDescription(edit.Description);
                group.Name = edit.Name!.Trim(); group.Description = edit.Description; group.Version++;
                payload = await access.ViewAsync(group, member, time.GetUtcNow(), ct); http.Response.Headers.ETag = CommandStore.ETag("group", group.Id, group.Version); break;
            case PolicyEdit edit:
                CommandStore.Match(http, CommandStore.ETag("policy", group.Id, group.PolicyVersion)); ValidatePolicy(edit.PotionPoints, edit.Letters);
                GroupAccess.PromotePolicy(group, time.GetUtcNow()); group.NextPotionPoints = edit.PotionPoints; group.NextLetters = edit.Letters; group.PolicyVersion++;
                payload = GroupAccess.Policies(group, time.GetUtcNow()); http.Response.Headers.ETag = CommandStore.ETag("policy", group.Id, group.PolicyVersion); break;
            case CurrencyEdit edit:
                ApiFault.Require(group.OwnerCharacterId == session.CharacterId || edit.Symbol == group.CurrencySymbol, 403, "owner_required", "General Moderation permits changing the currency name only.");
                CommandStore.Match(http, CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion));
                ApiFault.Text(edit.Name, 80); ApiFault.Text(edit.Symbol, 16, false); group.CurrencyName = edit.Name.Trim(); group.CurrencySymbol = edit.Symbol; group.CurrencyVersion++;
                payload = GroupAccess.Currency(group); http.Response.Headers.ETag = CommandStore.ETag("currency", group.CurrencyId, group.CurrencyVersion); break;
            case GroupDeletion deletion:
                await trades.CancelForAsync(groupId, null, "Group deleted.", ct);
                CommandStore.Match(http, CommandStore.ETag("group", group.Id, group.Version));
                ApiFault.Require(deletion.ConfirmName == group.Name, 422, "confirmation_required", "Confirm the exact group name.");
                group.Deleted = true; group.DeletedAt = time.GetUtcNow(); group.Version++;
                foreach (var invite in await db.Invitations.Where(x => x.GroupId == groupId && !x.Revoked).ToArrayAsync(ct)) invite.Revoked = true;
                foreach (var transfer in await db.OwnershipTransfers.Where(x => x.GroupId == groupId && x.Status == "proposed").ToArrayAsync(ct)) transfer.Status = "cancelled";
                break;
            default:
                ApiFault.Require(kind == "leave", 400, "invalid_request", "Unknown group command.");
                member.Status = "dormant"; member.Capabilities = []; member.Version++; group.Version++;
                await CancelTransfersForAsync(groupId, member.CharacterId, ct); break;
        }
        var response = payload == null ? (object)new Receipt(op.Id) : new CommandResult<object>(op.Id, payload);
        var result = await commands.SaveAsync(http, op, response, ct); await tx.CommitAsync(ct); return result;
    }
    private void ValidatePolicy(int points, int letters) => ApiFault.Require(points >= 0 && points <= policy.Value.MaxWeeklyPotionPoints && letters >= 0 && letters <= policy.Value.MaxWeeklyLetters,
        422, "invalid_request", "Weekly limits exceed supported bounds.");
    private async Task CancelTransfersForAsync(Guid group, Guid member, CancellationToken ct)
    {
        await trades.CancelForAsync(group, member, "Membership ended or trading was restricted.", ct);
        foreach (var transfer in await db.OwnershipTransfers.Where(x => x.GroupId == group && x.ToCharacterId == member && x.Status == "proposed").ToArrayAsync(ct)) transfer.Status = "cancelled";
    }

}
