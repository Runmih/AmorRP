using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;

namespace AmorRP.Server.Features.Billing;

public sealed partial class BillingService
{
    private static GroupBillingSettings ViewSettings(GroupRow group, GroupMembershipRow member) =>
        new(group.CommissionPercent, group.RequireClockIn, member.ClockedInAt,
            CommandStore.ETag("group-billing", group.Id, group.BillingSettingsVersion), member.CommissionPercent);

    public async Task<IResult> GroupSettingsAsync(HttpContext h, Guid seasonId, Guid groupId, GroupBillingEdit? body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        if (body == null)
        {
            await communities.RequireAsync(seasonId, actor, ct);
            var current = await GroupAsync(seasonId, groupId, ct);
            ApiFault.Require(!current.Deleted, 404, "not_found", "This group has disbanded.");
            var member = await GroupAuthorization.ActionAsync(db, current, actor, GroupCapabilities.Billing, ct);
            return Results.Json(ViewSettings(current, member));
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + seasonId], ct);
        await communities.RequireAsync(seasonId, actor, ct);
        var group = await GroupAsync(seasonId, groupId, ct);
        ApiFault.Require(!group.Deleted, 404, "not_found", "This group has disbanded.");
        GroupActivityService.Owner(group, actor);
        var owner = await GroupAuthorization.ActionAsync(db, group, actor, GroupCapabilities.Billing, ct);
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);

        CommandStore.Match(h, ViewSettings(group, owner).Etag);
        ApiFault.Require(body.CommissionPercent is >= 0 and <= 100, 422, "invalid_commission_rate",
            "Commission must be a whole percentage from 0 to 100.");
        group.CommissionPercent = body.CommissionPercent;
        group.RequireClockIn = body.RequireClockIn;
        group.BillingSettingsVersion++;
        var settings = ViewSettings(group, owner);
        h.Response.Headers.ETag = settings.Etag;
        var op = commands.New(h, scope, body, actor, seasonId, "group.billing_settings");
        op.Summary = $"Group billing settings saved: {group.CommissionPercent}% cashier commission.";
        var result = await commands.SaveAsync(h, op, new CommandResult<GroupBillingSettings>(op.Id, settings), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IResult> ClockInAsync(HttpContext h, Guid seasonId, Guid groupId, ClockInEdit body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + seasonId], ct);
        await communities.RequireAsync(seasonId, actor, ct);
        var group = await GroupAsync(seasonId, groupId, ct);
        ApiFault.Require(!group.Deleted, 404, "not_found", "This group has disbanded.");
        var member = await GroupAuthorization.ActionAsync(db, group, actor, GroupCapabilities.Billing, ct);
        var subject = member;
        if (body.CharacterId is { } target && target != actor)
        {
            GroupActivityService.Owner(group, actor);
            ApiFault.Require(!body.ClockedIn, 422, "invalid_clock_in", "A cashier must clock themselves in.");
            subject = await GroupAuthorization.MemberAsync(db, group, target, ct);
        }
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);

        subject.ClockedInAt = body.ClockedIn ? subject.ClockedInAt ?? time.GetUtcNow() : null;
        var op = commands.New(h, scope, body, actor, seasonId, "group.clock_in");
        op.SubjectCharacterId = subject.CharacterId;
        op.Summary = body.ClockedIn ? "Clocked in for group billing." : "Clocked out of group billing.";
        var result = await commands.SaveAsync(h, op, new CommandResult<GroupBillingSettings>(op.Id, ViewSettings(group, member)), ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
