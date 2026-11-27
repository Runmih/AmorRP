using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Combat;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Moodles;

public sealed class StatusEffectService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons, CommandStore commands, TimeProvider time)
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/status-effects", (HttpContext h, Guid seasonId, StatusEffectService service, CancellationToken ct) =>
            service.ReadAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/status-effects/{effectId}", (HttpContext h, Guid seasonId, Guid effectId, StatusEffect body, StatusEffectService service, CancellationToken ct) =>
            service.SaveAsync(h, seasonId, effectId, body, ct));
    }

    internal static StatusEffect? Decode(string? json) => json == null ? null : JsonSerializer.Deserialize<StatusEffect>(json);
    internal static string Encode(StatusEffect effect) => JsonSerializer.Serialize(effect);

    private async Task<IResult> ReadAsync(HttpContext http, Guid season, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(http, ct)).CharacterId;
        await seasons.RequireAsync(season, actor, ct);
        var latest = await db.StatusEffectRevisions.AsNoTracking().Where(row => row.CommunityId == season)
            .GroupBy(row => row.Id).Select(group => group.OrderByDescending(row => row.Revision).First()).ToArrayAsync(ct);
        return Results.Json(latest.Select(row => Decode(row.EffectJson)!).OrderBy(effect => effect.Name, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<IResult> SaveAsync(HttpContext http, Guid seasonId, Guid id, StatusEffect body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(http, ct)).CharacterId;
        var scope = "character:" + actor;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [scope, "season:" + seasonId], ct);
        var (season, _) = await seasons.RequireAsync(seasonId, actor, ct);
        SeasonAccess.Owner(season, actor);
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        ApiFault.Require(id != Guid.Empty && id == body.Id && PlainText.Error(body.Name, 80) == null &&
            body.DurationSeconds is >= 15 and <= 86400 && ValidModifier(body),
            422, "invalid_status_effect", "Enter a valid effect name, duration and modifier.");
        var current = await db.StatusEffectRevisions.Where(row => row.CommunityId == seasonId && row.Id == id)
            .MaxAsync(row => (int?)row.Revision, ct) ?? 0;
        ApiFault.Require(body.Revision == current, 409, "status_effect_changed", "Reload the status effect before saving.");
        if (current == 0)
        {
            var count = await db.StatusEffectRevisions.Where(row => row.CommunityId == seasonId)
                .Select(row => row.Id).Distinct().CountAsync(ct);
            ApiFault.Require(count < 256, 409, "status_library_full", "The season has reached its status effect limit.");
        }
        var visual = body.Moodle == null ? null : body.Moodle with { DurationSeconds = body.DurationSeconds };
        var updated = body with { Revision = current + 1, Name = body.Name.Trim(), Moodle = MoodleService.Decode(MoodleService.Encode(visual)) };
        db.StatusEffectRevisions.Add(new() { CommunityId = seasonId, Id = id, Revision = updated.Revision, EffectJson = Encode(updated) });
        var op = commands.New(http, scope, body, actor, seasonId, "status_effect.save");
        op.Summary = "Saved status effect " + updated.Name + ".";
        var result = await commands.SaveAsync(http, op, new CommandResult<StatusEffect>(op.Id, updated), ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static bool ValidModifier(StatusEffect effect) => effect.Modifier switch
    {
        "display" => effect.Amount == 0 && effect.Ability == null && effect.Moodle != null,
        "ac" or "attack" => effect.Amount is >= -10 and <= 10 and not 0 && effect.Ability == null,
        "check" => effect.Amount is >= -10 and <= 10 and not 0 && CombatRules.Abilities.Contains(effect.Ability!),
        _ => false
    };

    internal async Task<StatusEffect?> FindAsync(Guid season, StatusEffectReference? reference, CancellationToken ct)
    {
        if (reference == null) return null;
        var json = await db.StatusEffectRevisions.AsNoTracking()
            .Where(row => row.CommunityId == season && row.Id == reference.Id && row.Revision == reference.Revision)
            .Select(row => row.EffectJson).SingleOrDefaultAsync(ct);
        ApiFault.Require(json != null, 422, "unknown_status_effect", "Choose a saved status effect from this season.");
        return Decode(json);
    }

    internal async Task<bool> ApplyAsync(Guid season, Guid character, StatusEffect effect, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var current = await db.StatusGrants.SingleOrDefaultAsync(row => row.CommunityId == season
            && row.CharacterId == character && row.StatusEffectId == effect.Id, ct);
        if (current != null && current.ExpiresAt > now && Decode(current.EffectJson)!.Revision != effect.Revision)
            return false;
        if (current == null)
        {
            current = new() { CommunityId = season, CharacterId = character, StatusEffectId = effect.Id,
                Id = MoodleService.EffectId(season, character, "status-effect", effect.Id.ToString("N")) };
            db.StatusGrants.Add(current);
        }
        current.EffectJson = Encode(effect);
        current.ExpiresAt = now.AddSeconds(effect.DurationSeconds);
        return true;
    }

    internal async Task<int> ModifierAsync(Guid season, Guid character, string modifier, CancellationToken ct, string? ability = null)
    {
        var now = time.GetUtcNow();
        var effects = await db.StatusGrants.AsNoTracking().Where(row => row.CommunityId == season
            && row.CharacterId == character && row.ExpiresAt > now).Select(row => row.EffectJson).ToArrayAsync(ct);
        var bonus = 0;
        var penalty = 0;
        foreach (var json in effects)
        {
            var effect = Decode(json)!;
            if (effect.Modifier != modifier || effect.Ability != ability) continue;
            bonus = Math.Max(bonus, effect.Amount);
            penalty = Math.Min(penalty, effect.Amount);
        }
        return bonus + penalty;
    }
}
