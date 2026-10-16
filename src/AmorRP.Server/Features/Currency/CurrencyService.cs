using System.Globalization;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Currency;

public sealed class CurrencyService(AmorDbContext db, SessionAccess sessions, GroupAccess groups, CommandStore commands)
{
    public async Task<IResult> MemberBalanceAsync(HttpContext http, Guid groupId, Guid characterId, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); var (group, member) = await groups.RequireAsync(groupId, session.CharacterId, ct);
        if (characterId != session.CharacterId) GroupAccess.Action(group, member, "currency.manage");
        var target = await groups.TargetAsync(groupId, characterId, true, ct);
        return Results.Json(new BalancePage([GroupAccess.Balance(group, target)], null, target.BalanceVersion.ToString(), null));
    }
    public async Task<IResult> AdjustAsync(HttpContext http, Guid groupId, CurrencyAdjustment body, CancellationToken ct)
    {
        ApiFault.Text(body.Reason, 500);
        var valid = body.Delta != null && body.Delta.Length <= 20 && System.Text.RegularExpressions.Regex.IsMatch(body.Delta, "^-?[1-9][0-9]*$");
        ApiFault.Require(valid && long.TryParse(body.Delta, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), 422, "invalid_quantity", "Specify a nonzero signed 64-bit whole-unit delta.");
        var delta = long.Parse(body.Delta!, CultureInfo.InvariantCulture);
        var session = await sessions.GetAsync(http, ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "group:" + groupId], ct);
        var (group, member) = await groups.RequireAsync(groupId, session.CharacterId, ct); GroupAccess.Action(group, member, "currency.manage");
        var scope = "character:" + session.CharacterId; var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var target = await groups.TargetAsync(groupId, body.TargetCharacterId, true, ct);
        long after;
        try { after = checked(target.Owned + delta); }
        catch (OverflowException) { throw new ApiFault(409, "balance_overflow", "Balance would exceed the supported whole-unit range."); }
        ApiFault.Require(after >= target.Reserved, 409, "insufficient_available_quantity", "Not enough available currency.");
        var op = commands.New(http, scope, body, session.CharacterId, groupId, "currency.adjust");
        op.SubjectCharacterId = target.CharacterId; op.Delta = delta; op.BeforeBalance = target.Owned; op.AfterBalance = after;
        op.Reason = body.Reason; op.Summary = $"Currency adjustment: {delta.ToString(CultureInfo.InvariantCulture)}; balance {target.Owned.ToString(CultureInfo.InvariantCulture)} → {after.ToString(CultureInfo.InvariantCulture)}.";
        target.Owned = after; target.BalanceVersion++;
        var result = await commands.SaveAsync(http, op, new CommandResult<Balance>(op.Id, GroupAccess.Balance(group, target)), ct);
        await tx.CommitAsync(ct); return result;
    }
}
