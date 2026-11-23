using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Moodles;

public sealed class MoodleService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons, CommandStore commands, TimeProvider time)
{
    public static MoodleEffect? Decode(string? json) => json == null ? null : JsonSerializer.Deserialize<MoodleEffect>(json);

    public static string? Encode(MoodleEffect? effect, bool ownership = false)
    {
        if (effect == null) return null;
        var nameError = PlainText.Error(effect.Name, 80);
        var descriptionError = PlainText.Error(effect.Description, 300, multiline: true, required: false);
        ApiFault.Require(nameError == null && descriptionError == null &&
            !effect.Name!.Contains('[') && !(effect.Description ?? "").Contains('[') &&
            effect.IconId is >= 200000 and <= 299999 && effect.DurationSeconds <= 86400 &&
            effect.DurationSeconds >= 15 && (!ownership || effect.Debuff),
            422, "invalid_moodle", "Enter plain effect text, a current status icon ID, and a supported duration.");
        return JsonSerializer.Serialize(effect with { Name = effect.Name.Trim(), Description = effect.Description ?? "" });
    }

    // Identical authored effects refresh a single ID for this character and season.
    // Different saved revisions can still supply their independent effect data.
    internal static Guid EffectId(Guid season, Guid actor, string kind, string source) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"AmorRP:{season:N}:{actor:N}:{kind}:{source}")).AsSpan(0, 16));

    public async Task RefreshConsumableAsync(Guid season, Guid character, string effectJson, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var id = EffectId(season, character, "consumable", effectJson);
        await db.MoodleGrants.Where(x => x.CommunityId == season && x.CharacterId == character && x.ExpiresAt <= now && x.Id != id).ExecuteDeleteAsync(ct);
        var row = await db.MoodleGrants.SingleOrDefaultAsync(x => x.CommunityId == season && x.CharacterId == character && x.Id == id, ct);
        if (row == null)
        {
            row = new MoodleGrantRow { CommunityId = season, CharacterId = character, Id = id, EffectJson = effectJson };
            db.MoodleGrants.Add(row);
        }
        row.ExpiresAt = now.AddSeconds(Decode(effectJson)!.DurationSeconds);
    }

    public async Task<IResult> ReadAsync(HttpContext h, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var now = time.GetUtcNow();
        var season = await db.Seasons.AsNoTracking().SingleOrDefaultAsync(s => !s.Deleted && s.SeasonState == "active", ct);
        var consumables = await db.MoodleGrants.AsNoTracking()
            .Where(x => x.CharacterId == actor && x.ExpiresAt > now).ToArrayAsync(ct);
        var permits = await db.OwnershipPermits.AsNoTracking().Where(x => x.CharacterId == actor).ToArrayAsync(ct);
        var active = new List<MoodleGrant>();
        var remove = new List<Guid>();
        foreach (var row in consumables)
        {
            if (row.CommunityId == season?.Id) active.Add(new(row.Id, Decode(row.EffectJson)!, row.ExpiresAt));
            else remove.Add(row.Id);
        }
        var ownershipEffect = Decode(season?.OwnershipEffectJson);
        foreach (var permit in permits)
        {
            var id = EffectId(permit.CommunityId, actor, "ownership", permit.Id.ToString("N"));
            if (permit.CommunityId == season?.Id && permit.ReleasedAt == null && ownershipEffect != null)
                active.Add(new(id, ownershipEffect, null));
            else remove.Add(id);
        }
        h.Response.Headers.CacheControl = "no-store";
        return Results.Json(new MoodleGrants(active.ToArray(), remove.ToArray(), now));
    }

    public async Task<IResult> PolicyAsync(HttpContext h, Guid id, OwnershipEffectPolicy? edit, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        if (edit == null)
        {
            var (season, _) = await seasons.RequireAsync(id, actor, ct);
            SeasonAccess.Owner(season, actor);
            return Results.Json(new OwnershipEffectPolicy(season.OwnershipEffectVersion, Decode(season.OwnershipEffectJson)));
        }

        var scope = "character:" + actor;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [scope, "season:" + id], ct);
        var (editing, _) = await seasons.RequireAsync(id, actor, ct);
        SeasonAccess.Owner(editing, actor);
        var replay = await commands.ReplayAsync(h, scope, edit, ct);
        if (replay != null) return commands.Replay(replay);
        ApiFault.Require(edit.Version == editing.OwnershipEffectVersion, 409, "ownership_effect_changed", "Reload the ownership effect before saving.");
        editing.OwnershipEffectJson = Encode(edit.Effect, ownership: true);
        editing.OwnershipEffectVersion++;
        var op = commands.New(h, scope, edit, actor, id, "moodle.ownership_policy");
        op.Summary = "Updated the ownership permit effect.";
        var result = await commands.SaveAsync(h, op, new CommandResult<OwnershipEffectPolicy>(op.Id,
            new(editing.OwnershipEffectVersion, Decode(editing.OwnershipEffectJson))), ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
