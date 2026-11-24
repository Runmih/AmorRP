using AmorRP.Contracts.Common;
using AmorRP.Contracts.Seasons;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Trading;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Seasons;

public sealed class SeasonService(AmorDbContext db, SessionAccess sessions, CommandStore commands,
    TradeReservations trades, IOptions<SeasonPolicyOptions> policy, PageCursor pages)
{
    public async Task<string[]> CapabilitiesAsync(Guid actor, CancellationToken ct) =>
        await IsAdministratorAsync(actor, ct) || await db.SeasonCreationPermissions.AnyAsync(x => x.CharacterId == actor && x.CanCreate, ct)
            ? [SeasonCapabilities.Create] : [];

    public async Task RequireCreationAsync(Guid actor, CancellationToken ct) =>
        ApiFault.Require((await CapabilitiesAsync(actor, ct)).Contains(SeasonCapabilities.Create), 403, "season_creation_required", "Creating a season requires the seasons.create permission.");

    public async Task<bool> IsAdministratorAsync(Guid actor, CancellationToken ct)
    {
        var lodestone = await db.Characters.Where(x => x.Id == actor).Select(x => x.LodestoneId).SingleAsync(ct);
        return policy.Value.AdministratorLodestoneIds.Contains(lodestone, StringComparer.Ordinal);
    }

    public async Task RequireAdministratorAsync(Guid actor, CancellationToken ct) =>
        ApiFault.Require(await IsAdministratorAsync(actor, ct), 403, "season_administrator_required", "Only a configured server administrator may manage seasons.");

    public Task EnsureMembershipAsync(Guid actor, CancellationToken ct) =>
        SeasonMemberships.EnrollAsync(db, actor, null, ct);

    public async Task<IResult> ContextAsync(HttpContext http, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        await EnsureMembershipAsync(session.CharacterId, ct);
        var admin = await IsAdministratorAsync(session.CharacterId, ct);
        var active = await db.Seasons.Where(x => x.SeasonState == "active" && !x.Deleted).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var capabilities = await CapabilitiesAsync(session.CharacterId, ct);
        var seasons = (admin || capabilities.Contains(SeasonCapabilities.Create)) ? await db.Seasons.AsNoTracking()
            .Where(x => !x.Deleted && (admin || x.CreatedByCharacterId == session.CharacterId)).OrderByDescending(x => x.Id)
            .Select(x => new SeasonSummary(x.Id, x.Name, x.SeasonState, x.Version)).ToArrayAsync(ct) : [];
        return Results.Json(new SeasonContext(admin, active, seasons, capabilities));
    }

    public async Task<IResult> CreationPermissionsAsync(HttpContext http, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(http, ct)).CharacterId;
        await RequireAdministratorAsync(actor, ct);
        var search = http.Request.Query["search"].ToString().Trim();
        ApiFault.Require(search.Length <= 200, 422, "invalid_request", "Search must be 200 characters or fewer.");
        var scope = $"season-creation:{actor}:{SecretVault.Hash(search)}";
        var (limit, after) = pages.Read(http, scope);
        var query = db.Characters.AsNoTracking();
        if (search.Length > 0)
        {
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(x => EF.Functions.ILike(x.DisplayName + " " + x.HomeWorldName, pattern, "\\"));
        }
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var characters = await query.OrderBy(x => x.Id).Take(limit + 1).ToArrayAsync(ct);
        var ids = characters.Take(limit).Select(x => x.Id).ToArray();
        var grants = await db.SeasonCreationPermissions.AsNoTracking().Where(x => ids.Contains(x.CharacterId)).ToDictionaryAsync(x => x.CharacterId, ct);
        var items = characters.Take(limit).Select(x => Permission(x, grants.GetValueOrDefault(x.Id))).ToArray();
        return Results.Json(new SeasonCreationPermissionPage(items, characters.Length > limit ? pages.Write(characters[limit - 1].Id, scope) : null));
    }

    private SeasonCreationPermission Permission(CharacterRow character, SeasonCreationPermissionRow? grant)
    {
        var admin = policy.Value.AdministratorLodestoneIds.Contains(character.LodestoneId, StringComparer.Ordinal);
        return new(SessionAccess.View(character), admin || grant?.CanCreate == true, admin, grant?.Version ?? 0,
            CommandStore.ETag("season-creation", character.Id, grant?.Version ?? 0));
    }

    public async Task<IResult> CreationPermissionAsync(HttpContext http, Guid target, SeasonCreationPermissionEdit body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(http, ct)).CharacterId;
        await RequireAdministratorAsync(actor, ct);
        ApiFault.Text(body.Reason, 500);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "character:" + target], ct);
        var replay = await commands.ReplayAsync(http, "character:" + actor, body, ct);
        if (replay != null) return commands.Replay(replay);
        var character = await db.Characters.SingleOrDefaultAsync(x => x.Id == target, ct);
        ApiFault.Require(character != null, 404, "not_found", "Verified character not found.");
        var grant = await db.SeasonCreationPermissions.SingleOrDefaultAsync(x => x.CharacterId == target, ct);
        var before = Permission(character!, grant);
        ApiFault.Require(!before.IsAdministrator, 409, "administrator_permission_automatic", "Administrator season creation permission is automatic.");
        CommandStore.Match(http, before.Etag);
        if (grant == null) { grant = new() { CharacterId = target }; db.SeasonCreationPermissions.Add(grant); }
        else grant.Version++;
        grant.CanCreate = body.CanCreate;
        var op = commands.New(http, "character:" + actor, body, actor, null, "season.creation-permission");
        op.SubjectCharacterId = target; op.Reason = body.Reason;
        op.Summary = body.CanCreate ? "Granted seasons.create." : "Revoked seasons.create.";
        var result = Permission(character!, grant);
        http.Response.Headers.ETag = result.Etag;
        var response = await commands.SaveAsync(http, op, new CommandResult<SeasonCreationPermission>(op.Id, result), ct);
        await tx.CommitAsync(ct); return response;
    }

    public async Task<IResult> TransitionAsync(HttpContext http, Guid id, SeasonTransition body, bool activate, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        await RequireAdministratorAsync(session.CharacterId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize lifecycle decisions first, then lock all affected economies in canonical order.
        await CommandStore.LockAsync(db, ["season-lifecycle"], ct);
        var activeId = await db.Seasons.Where(x => x.SeasonState == "active").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        await CommandStore.LockAsync(db, new[] { id, activeId ?? id }.Select(x => "season:" + x).Append("character:" + session.CharacterId), ct);
        var season = await db.Seasons.SingleOrDefaultAsync(x => x.Id == id && !x.Deleted, ct);
        ApiFault.Require(season != null, 404, "not_found", "Season not found.");
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        CommandStore.Match(http, CommandStore.ETag("season", id, season!.Version));
        ApiFault.Require(body.ConfirmName == season.Name, 422, "confirmation_required", "Type the season name exactly to confirm the transition.");
        ApiFault.Require(activate ? season.SeasonState == "draft" : season.SeasonState == "active", 409, "invalid_season_transition", "Only a draft season can be activated. Archived seasons cannot be reopened.");
        if (activate && activeId.HasValue)
            await ArchiveAsync(await db.Seasons.SingleAsync(x => x.Id == activeId, ct), ct);
        if (activate)
        {
            // Flush the former active state before setting the new one (unique active-season index).
            await db.SaveChangesAsync(ct);
            season.SeasonState = "active"; season.Version++;
            await SeasonMemberships.EnrollAsync(db, null, id, ct);
        }
        else await ArchiveAsync(season, ct);
        var op = commands.New(http, scope, body, session.CharacterId, id, activate ? "season.activate" : "season.archive");
        http.Response.Headers.ETag = CommandStore.ETag("season", id, season.Version);
        var result = await commands.SaveAsync(http, op, new CommandResult<SeasonSummary>(op.Id, new(id, season.Name, season.SeasonState, season.Version)), ct);
        await tx.CommitAsync(ct); return result;
    }

    private async Task ArchiveAsync(CommunityRow season, CancellationToken ct)
    {
        // Release reservations and settle released funds under the old rules before freezing records.
        await trades.CancelForAsync(season.Id, null, "Season archived.", ct);
        await AmorRP.Server.Features.Discord.DiscordRewardsService.DisconnectSeasonAsync(db, season.Id, ct);
        await db.PermitTransfers.Where(x => x.CommunityId == season.Id && (x.State == "pending" || x.State == "approved"))
            .ExecuteUpdateAsync(fields => fields.SetProperty(x => x.State, "cancelled"), ct);
        foreach (var invite in await db.Invitations.Where(x => x.CommunityId == season.Id && !x.Revoked).ToArrayAsync(ct)) invite.Revoked = true;
        foreach (var transfer in await db.OwnershipTransfers.Where(x => x.CommunityId == season.Id && x.Status == "proposed").ToArrayAsync(ct)) transfer.Status = "cancelled";
        season.SeasonState = "archived"; season.Version++;
    }
}
