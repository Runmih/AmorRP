using System.Globalization;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Currency;

public sealed class CurrencyService(AmorDbContext db, SessionAccess sessions, CommunityAccess communities, CommandStore commands)
{
    public async Task<IResult> MemberBalanceAsync(HttpContext http, Guid communityId, Guid characterId, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct); var (community, member) = await communities.RequireAsync(communityId, session.CharacterId, ct);
        if (characterId != session.CharacterId) CommunityAccess.Action(community, member, "currency.manage");
        var target = await communities.TargetAsync(communityId, characterId, true, ct);
        return Results.Json(new BalancePage([CommunityAccess.Balance(community, target)], null, target.BalanceVersion.ToString(), null));
    }
    public async Task<IResult> AdjustAsync(HttpContext http, Guid communityId, CurrencyAdjustment body, CancellationToken ct)
    {
        ApiFault.Text(body.Reason, 500);
        var valid = body.Delta != null && body.Delta.Length <= 20 && System.Text.RegularExpressions.Regex.IsMatch(body.Delta, "^-?[1-9][0-9]*$");
        ApiFault.Require(valid && long.TryParse(body.Delta, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), 422, "invalid_quantity", "Specify a nonzero signed 64-bit whole-unit delta.");
        var delta = long.Parse(body.Delta!, CultureInfo.InvariantCulture);
        var session = await sessions.GetAsync(http, ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "community:" + communityId], ct);
        var (community, member) = await communities.RequireAsync(communityId, session.CharacterId, ct); CommunityAccess.Action(community, member, "currency.manage");
        var scope = "character:" + session.CharacterId; var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var target = await communities.TargetAsync(communityId, body.TargetCharacterId, true, ct);
        long after;
        try { after = checked(target.Owned + delta); }
        catch (OverflowException) { throw new ApiFault(409, "balance_overflow", "Balance would exceed the supported whole-unit range."); }
        ApiFault.Require(after >= target.Reserved, 409, "insufficient_available_quantity", "Not enough available currency.");
        var op = commands.New(http, scope, body, session.CharacterId, communityId, "currency.adjust");
        op.SubjectCharacterId = target.CharacterId; op.Delta = delta; op.BeforeBalance = target.Owned; op.AfterBalance = after;
        op.Reason = body.Reason; op.Summary = $"Currency adjustment: {delta.ToString(CultureInfo.InvariantCulture)}; balance {target.Owned.ToString(CultureInfo.InvariantCulture)} → {after.ToString(CultureInfo.InvariantCulture)}.";
        target.Owned = after; target.BalanceVersion++;
        var result = await commands.SaveAsync(http, op, new CommandResult<Balance>(op.Id, CommunityAccess.Balance(community, target)), ct);
        await tx.CommitAsync(ct); return result;
    }
}
