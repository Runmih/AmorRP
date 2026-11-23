using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Gathering;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using AmorRP.Server.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmorRP.Server.Features.Seasons;

public sealed partial class SeasonResourceService(AmorDbContext db, SessionAccess sessions, SeasonAccess access, CommandStore commands,
    PageCursor pages, IOptions<ServicePolicyOptions> policy, TimeProvider time, AmorRP.Server.Features.Trading.TradeReservations trades, SeasonService seasons, GatheringService gathering)
{
    public async Task<IResult> ListAsync(HttpContext http, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        await seasons.EnsureMembershipAsync(session.CharacterId, ct);
        var rows = await (from m in db.Memberships join g in db.Seasons on m.CommunityId equals g.Id
            where m.CharacterId == session.CharacterId && m.Status == "active" && !g.Deleted orderby g.Id select new { G = g, M = m }).ToArrayAsync(ct);
        var items = new List<Season>();
        foreach (var row in rows) items.Add(await access.ViewAsync(row.G, row.M, time.GetUtcNow(), ct));
        return Results.Json(new SeasonPage(items.ToArray(), null, string.Join('-', items.Select(x => $"{x.Id:N}:{x.Version}")), null));
    }
    private static void ValidateDescription(string description) => ApiFault.Require(AmorRP.Core.Items.PlainText.Error(description, SeasonEdit.MaximumDescriptionCharacters, multiline: true, required: false) == null, 422, "invalid_request", "Season description must contain at most 300 plain-text characters.");

    public async Task<IResult> CreateAsync(HttpContext http, SeasonCreate body, CancellationToken ct)
    {
        ApiFault.Text(body.Name, 80); ValidateDescription(body.Description); ApiFault.Text(body.CurrencyName, 80); ApiFault.Text(body.CurrencySymbol, 16, false);
        ValidatePolicy(body.WeeklyPotionPoints, body.WeeklyLetters);
        ApiFault.Require(body.WeeklyCookingPoints >= 0 && body.WeeklyCookingPoints <= policy.Value.MaxWeeklyPotionPoints, 422, "invalid_policy", "Cooking points exceed the supported range.");
        var session = await sessions.GetAsync(http, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId], ct);
        await seasons.RequireCreationAsync(session.CharacterId, ct);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null)
        {
            if (replay.CommunityId.HasValue) await access.RequireAsync(replay.CommunityId.Value, session.CharacterId, ct);
            return commands.Replay(replay);
        }
        ApiFault.Require(await db.Seasons.CountAsync(x => x.CreatedByCharacterId == session.CharacterId, ct) < policy.Value.MaxRetainedSeasonsPerCreator,
            409, "season_retention_limit_reached", $"Retained season creation limit ({policy.Value.MaxRetainedSeasonsPerCreator}) reached. All retained seasons count until eligible data is purged by an operator.");
        var community = new CommunityRow { Name = body.Name.Trim(), Description = body.Description, OwnerCharacterId = session.CharacterId,
            SeasonState = "draft",
            CreatedByCharacterId = session.CharacterId,
            CurrencyName = body.CurrencyName.Trim(), CurrencySymbol = body.CurrencySymbol, PolicyPeriod = SeasonAccess.Period(time.GetUtcNow()),
            CurrentPotionPoints = body.WeeklyPotionPoints, NextPotionPoints = body.WeeklyPotionPoints,
            CurrentCookingPoints = body.WeeklyCookingPoints, NextCookingPoints = body.WeeklyCookingPoints,
            CurrentLetters = body.WeeklyLetters, NextLetters = body.WeeklyLetters };
        var member = new MembershipRow { CommunityId = community.Id, CharacterId = session.CharacterId };
        db.Seasons.Add(community); AmorRP.Server.Features.Catalog.CatalogService.Seed(db, community.Id); db.Memberships.Add(member);
        await db.SaveChangesAsync(ct);
        await SeasonMemberships.EnrollAsync(db, null, community.Id, ct);
        var op = commands.New(http, scope, body, session.CharacterId, community.Id, "season.create");
        http.Response.Headers.ETag = CommandStore.ETag("season", community.Id, community.Version);
        var result = await commands.SaveAsync(http, op, new CommandResult<Season>(op.Id, await access.ViewAsync(community, member, time.GetUtcNow(), ct)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> ReadAsync(HttpContext http, Guid communityId, string kind, CancellationToken ct)
    {
        var session = await sessions.GetAsync(http, ct);
        var (community, member) = await access.RequireAsync(communityId, session.CharacterId, ct);
        object response;
        switch (kind)
        {
            case "policies": response = SeasonAccess.Policies(community, time.GetUtcNow()); http.Response.Headers.ETag = CommandStore.ETag("policy", community.Id, community.PolicyVersion); break;
            case "currency": response = SeasonAccess.Currency(community); http.Response.Headers.ETag = CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion); break;
            case "balances": response = new BalancePage([SeasonAccess.Balance(community, member)], null, member.BalanceVersion.ToString(), null); break;
            default: response = await access.ViewAsync(community, member, time.GetUtcNow(), ct); http.Response.Headers.ETag = CommandStore.ETag("season", community.Id, community.Version); break;
        }
        return Results.Json(response);
    }
    public async Task<IResult> EditAsync(HttpContext http, Guid communityId, object? body, string kind, CancellationToken ct)
    {
        ApiFault.Require(kind is "edit" or "currency" or "policies", 400, "invalid_request", "Unknown season command.");
        var session = await sessions.GetAsync(http, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + session.CharacterId, "season:" + communityId], ct);
        var (community, member) = await access.RequireAsync(communityId, session.CharacterId, ct);
        if (kind is "edit" or "currency") SeasonAccess.Action(community, member, "season.moderate");
        else SeasonAccess.Owner(community, session.CharacterId);
        var scope = "character:" + session.CharacterId;
        var replay = await commands.ReplayAsync(http, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var op = commands.New(http, scope, body, session.CharacterId, communityId, "season." + kind);
        object? payload = null;
        switch (body)
        {
            case SeasonEdit edit:
                ApiFault.Require(community.OwnerCharacterId == session.CharacterId || string.Equals(edit.Name?.Trim(), community.Name, StringComparison.Ordinal), 403, "owner_required", "Only the owner may rename the season.");
                CommandStore.Match(http, CommandStore.ETag("season", community.Id, community.Version));
                ApiFault.Text(edit.Name, 80); ValidateDescription(edit.Description);
                community.Name = edit.Name!.Trim(); community.Description = edit.Description; community.Version++;
                payload = await access.ViewAsync(community, member, time.GetUtcNow(), ct); http.Response.Headers.ETag = CommandStore.ETag("season", community.Id, community.Version); break;
            case PolicyEdit edit:
                CommandStore.Match(http, CommandStore.ETag("policy", community.Id, community.PolicyVersion)); ValidatePolicy(edit.PotionPoints, edit.Letters);
                ApiFault.Require(edit.CookingPoints >= 0 && edit.CookingPoints <= policy.Value.MaxWeeklyPotionPoints, 422, "invalid_policy", "Cooking points exceed the supported range.");
                var now = time.GetUtcNow();
                SeasonAccess.PromotePolicy(community, now);
                var gatheringSettings = edit.Gathering == null ? null : await gathering.ApplySettingsAsync(community, edit.Gathering, now, ct);
                community.NextCookingPoints = edit.CookingPoints;
                community.NextPotionPoints = edit.PotionPoints;
                community.NextLetters = edit.Letters;
                community.PolicyVersion++;
                payload = SeasonAccess.Policies(community, now) with { Gathering = gatheringSettings };
                http.Response.Headers.ETag = CommandStore.ETag("policy", community.Id, community.PolicyVersion);
                break;
            case CurrencyEdit edit:
                ApiFault.Require(community.OwnerCharacterId == session.CharacterId || edit.Symbol == community.CurrencySymbol, 403, "owner_required", "General Moderation permits changing the currency name only.");
                CommandStore.Match(http, CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion));
                ApiFault.Text(edit.Name, 80); ApiFault.Text(edit.Symbol, 16, false); community.CurrencyName = edit.Name.Trim(); community.CurrencySymbol = edit.Symbol; community.CurrencyVersion++;
                payload = SeasonAccess.Currency(community); http.Response.Headers.ETag = CommandStore.ETag("currency", community.CurrencyId, community.CurrencyVersion); break;
            default:
                ApiFault.Require(false, 400, "invalid_request", "Unknown season command."); break;
        }
        var response = payload == null ? (object)new Receipt(op.Id) : new CommandResult<object>(op.Id, payload);
        var result = await commands.SaveAsync(http, op, response, ct); await tx.CommitAsync(ct); return result;
    }
    private void ValidatePolicy(int points, int letters) => ApiFault.Require(points >= 0 && points <= policy.Value.MaxWeeklyPotionPoints && letters >= 0 && letters <= policy.Value.MaxWeeklyLetters,
        422, "invalid_request", "Weekly limits exceed supported bounds.");

}
