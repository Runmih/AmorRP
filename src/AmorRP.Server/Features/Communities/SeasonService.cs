using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Communities;

public sealed class SeasonService(AmorDbContext db, SessionAccess sessions, CommandStore commands,
    TradeReservations trades, IOptions<SeasonPolicyOptions> policy)
{
    public bool Enabled => policy.Value.Enabled;
    public async Task<bool> IsAdministratorAsync(Guid actor, CancellationToken ct)
    {
        var lodestone = await db.Characters.Where(x => x.Id == actor).Select(x => x.LodestoneId).SingleAsync(ct);
        return policy.Value.AdministratorLodestoneIds.Contains(lodestone, StringComparer.Ordinal);
    }

    public async Task RequireAdministratorAsync(Guid actor, CancellationToken ct) =>
        ApiFault.Require(Enabled && await IsAdministratorAsync(actor, ct), 403, "season_administrator_required", "Only a configured server administrator may manage seasons.");

    // Call with the community lock held. Missing memberships are enrolled; moderation decisions
    // (dormant/blocked rows) are never undone by signing in or refreshing.
    private async Task EnrollAsync(Guid season, Guid? actor, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO memberships ("CommunityId", "CharacterId", "Status", "ReadAnnouncementIds", "Description", "Capabilities", "TradingRestricted", "Version", "LastCollectionAttempt", "CollectionPending", "Owned", "Reserved", "BalanceVersion")
            SELECT {season}, "Id", 'active', ARRAY[]::uuid[], '', ARRAY[]::text[], false, 1, 0, false, 0, 0, 1
            FROM characters WHERE ({actor}::uuid IS NULL OR "Id" = {actor})
            ON CONFLICT ("CommunityId", "CharacterId") DO NOTHING
            """, ct);

    public async Task EnsureMembershipAsync(Guid actor, CancellationToken ct)
    {
        if (!Enabled) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["season-lifecycle"], ct);
        var active = await db.Communities.Where(x => x.SeasonState == "active" && !x.Deleted).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (active.HasValue)
        {
            await CommandStore.LockAsync(db, ["community:" + active], ct);
            await EnrollAsync(active.Value, actor, ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<IResult> ContextAsync(HttpContext http, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        await EnsureMembershipAsync(session.CharacterId, ct);
        var admin = Enabled && await IsAdministratorAsync(session.CharacterId, ct);
        var active = await db.Communities.Where(x => x.SeasonState == "active" && !x.Deleted).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var seasons = admin ? await db.Communities.AsNoTracking().Where(x => !x.Deleted).OrderByDescending(x => x.Id)
            .Select(x => new SeasonSummary(x.Id, x.Name, x.SeasonState, x.Version)).ToArrayAsync(ct) : [];
        return Results.Json(new SeasonContext(Enabled, admin, active, seasons));
    }

    public async Task<IResult> TransitionAsync(HttpContext http, Guid id, SeasonTransition body, bool activate, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        await RequireAdministratorAsync(session.CharacterId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize lifecycle decisions first, then lock all affected economies in canonical order.
        await CommandStore.LockAsync(db, ["season-lifecycle"], ct);
        var activeId = await db.Communities.Where(x => x.SeasonState == "active").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        await CommandStore.LockAsync(db, new[] { id, activeId ?? id }.Select(x => "community:" + x).Append("character:" + session.CharacterId), ct);
        var season = await db.Communities.SingleOrDefaultAsync(x => x.Id == id && !x.Deleted, ct);
        ApiFault.Require(season != null, 404, "not_found", "Season not found.");
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        CommandStore.Match(http, CommandStore.ETag("community", id, season!.Version));
        ApiFault.Require(body.ConfirmName == season.Name, 422, "confirmation_required", "Type the season name exactly to confirm the transition.");
        ApiFault.Require(activate ? season.SeasonState is "draft" or "legacy" : season.SeasonState == "active", 409, "invalid_season_transition", "Only a draft or existing legacy community can be activated. Archived seasons cannot be reopened.");
        if (activate && activeId.HasValue)
            await ArchiveAsync(await db.Communities.SingleAsync(x => x.Id == activeId, ct), ct);
        if (activate)
        {
            // Flush the former active state before setting the new one (unique active-season index).
            await db.SaveChangesAsync(ct);
            season.SeasonState = "active"; season.Version++;
            await EnrollAsync(id, null, ct);
        }
        else await ArchiveAsync(season, ct);
        var op = commands.New(http, scope, body, session.CharacterId, id, activate ? "season.activate" : "season.archive");
        http.Response.Headers.ETag = CommandStore.ETag("community", id, season.Version);
        var result = await commands.SaveAsync(http, op, new CommandResult<SeasonSummary>(op.Id, new(id, season.Name, season.SeasonState, season.Version)), ct);
        await tx.CommitAsync(ct); return result;
    }

    private async Task ArchiveAsync(CommunityRow season, CancellationToken ct)
    {
        // Release reservations and settle released funds under the old rules before freezing records.
        await trades.CancelForAsync(season.Id, null, "Season archived.", ct);
        foreach (var invite in await db.Invitations.Where(x => x.CommunityId == season.Id && !x.Revoked).ToArrayAsync(ct)) invite.Revoked = true;
        foreach (var transfer in await db.OwnershipTransfers.Where(x => x.CommunityId == season.Id && x.Status == "proposed").ToArrayAsync(ct)) transfer.Status = "cancelled";
        season.SeasonState = "archived"; season.Version++;
    }
}
