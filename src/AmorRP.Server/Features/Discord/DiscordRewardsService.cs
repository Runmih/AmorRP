using System.Security.Cryptography;
using System.Text;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Discord;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Billing;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Discord;

public sealed partial class DiscordRewardsService(AmorDbContext db, SessionAccess sessions, SeasonAccess seasons,
    CommandStore commands, DebtSettlement settlement, IConfiguration config, TimeProvider time)
{
    private const string LinkLock = "discord:links";
    private const string ActiveSeason = "active";
    private const string GuildAccess = "Discord Bot Permission";
    private string? ServiceKey => config["DiscordRewards:ServiceKey"];
    private bool Configured => ServiceKey is { Length: >= 32 and <= 256 };

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v2/seasons/{seasonId}/discord", (HttpContext h, Guid seasonId, DiscordRewardsService s, CancellationToken ct) => s.StateAsync(h, seasonId, ct));
        app.MapPut("/api/v2/seasons/{seasonId}/discord/policy", (HttpContext h, Guid seasonId, DiscordRewardPolicy body, DiscordRewardsService s, CancellationToken ct) => s.PolicyAsync(h, seasonId, body, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/discord/link", (HttpContext h, Guid seasonId, DiscordRewardsService s, CancellationToken ct) => s.CreateCodeAsync(h, seasonId, null, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/discord/link", (HttpContext h, Guid seasonId, DiscordRewardsService s, CancellationToken ct) => s.UnlinkPlayerAsync(h, seasonId, ct));
        app.MapGet("/api/v2/seasons/{seasonId}/groups/{groupId}/discord", (HttpContext h, Guid seasonId, Guid groupId, DiscordRewardsService s, CancellationToken ct) => s.GroupStateAsync(h, seasonId, groupId, ct));
        app.MapPost("/api/v2/seasons/{seasonId}/groups/{groupId}/discord/link", (HttpContext h, Guid seasonId, Guid groupId, DiscordRewardsService s, CancellationToken ct) => s.CreateCodeAsync(h, seasonId, groupId, ct));
        app.MapDelete("/api/v2/seasons/{seasonId}/groups/{groupId}/discord/link", (HttpContext h, Guid seasonId, Guid groupId, DiscordRewardsService s, CancellationToken ct) => s.UnlinkGroupAsync(h, seasonId, groupId, ct));

        app.MapPost("/api/v2/integrations/discord/guilds/redeem", (HttpContext h, DiscordRedeem body, DiscordRewardsService s, CancellationToken ct) => s.RedeemAsync(h, body, true, ct));
        app.MapPost("/api/v2/integrations/discord/players/redeem", (HttpContext h, DiscordRedeem body, DiscordRewardsService s, CancellationToken ct) => s.RedeemAsync(h, body, false, ct));
        app.MapPut("/api/v2/integrations/discord/guilds/{guildId}/channels", (HttpContext h, string guildId, DiscordChannels body, DiscordRewardsService s, CancellationToken ct) => s.ConfigureGuildAsync(h, guildId, body, ct));
        app.MapGet("/api/v2/integrations/discord/guilds", (HttpContext h, DiscordRewardsService s, CancellationToken ct) => s.GuildsAsync(h, ct));
        app.MapDelete("/api/v2/integrations/discord/guilds/{guildId}", (HttpContext h, string guildId, DiscordRewardsService s, CancellationToken ct) => s.RemovedAsync(h, guildId, ct));
        app.MapPost("/api/v2/integrations/discord/posts", (HttpContext h, DiscordPost body, DiscordRewardsService s, CancellationToken ct) => s.PostAsync(h, body, ct));
    }

    private void RequireBot(HttpContext h)
    {
        var supplied = h.Request.Headers["X-AmorRP-Discord-Key"].ToString();
        ApiFault.Require(Configured && supplied.Length is >= 32 and <= 256
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
                SHA256.HashData(Encoding.UTF8.GetBytes(ServiceKey!))), 401, "discord_service_required", "Discord service authorization required.");
    }

    private static bool IsSnowflake(string? value) => value is { Length: >= 16 and <= 20 }
        && value.All(char.IsAsciiDigit) && value[0] != '0' && long.TryParse(value, out _);

    private async Task<IResult> StateAsync(HttpContext h, Guid seasonId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var (season, _) = await seasons.RequireAsync(seasonId, actor, ct);
        var policy = await db.DiscordPolicies.FindAsync([seasonId], ct);
        var linked = await db.DiscordPlayers.AnyAsync(x => x.CommunityId == seasonId && x.CharacterId == actor, ct);
        return Results.Json(new DiscordPlayerState(Configured && policy?.Enabled == true, linked,
            season.OwnerCharacterId == actor ? View(policy) : null));
    }

    private async Task<IResult> GroupStateAsync(HttpContext h, Guid seasonId, Guid groupId, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await seasons.RequireAsync(seasonId, actor, ct);
        await OwnedGroupAsync(seasonId, groupId, actor, ct);
        var guild = await db.DiscordGuilds.SingleOrDefaultAsync(x => x.CommunityId == seasonId && x.GroupId == groupId && x.Active, ct);
        return Results.Json(new DiscordGroupState(guild != null, guild?.GuildId));
    }

    private static DiscordRewardPolicy View(DiscordPolicyRow? row) => row == null ? new()
        : new(row.Enabled, row.CoinsPerPost, row.MinimumCharacters, row.CooldownSeconds, row.WeeklyLimit, row.SeasonWeeklyBudget, row.BalanceCeiling);

    private Task<IResult> PolicyAsync(HttpContext h, Guid seasonId, DiscordRewardPolicy body, CancellationToken ct) =>
        CharacterCommandAsync(h, seasonId, body, "discord.policy", async (season, actor) =>
        {
            SeasonAccess.Owner(season, actor);
            ApiFault.Require(body.CoinsPerPost is >= 1 and <= 10000 && body.MinimumCharacters is >= 10 and <= 2000
                && body.CooldownSeconds is >= 10 and <= 86400 && body.WeeklyLimit is >= 1 and <= 1000000
                && body.SeasonWeeklyBudget is >= 1 and <= 100000000 && body.BalanceCeiling is >= 1 and <= long.MaxValue,
                422, "invalid_discord_policy", "Choose positive reward limits and a cooldown of at least 10 seconds.");
            ApiFault.Require(!body.Enabled || Configured, 409, "discord_not_configured", "Set up the Discord service before enabling rewards.");
            var row = await db.DiscordPolicies.FindAsync([seasonId], ct);
            if (row == null)
            {
                row = new() { CommunityId = seasonId };
                db.DiscordPolicies.Add(row);
            }
            row.Enabled = body.Enabled;
            row.CoinsPerPost = body.CoinsPerPost;
            row.MinimumCharacters = body.MinimumCharacters;
            row.CooldownSeconds = body.CooldownSeconds;
            row.WeeklyLimit = body.WeeklyLimit;
            row.SeasonWeeklyBudget = body.SeasonWeeklyBudget;
            row.BalanceCeiling = body.BalanceCeiling;
            return View(row);
        }, ct);

    private Task<IResult> CreateCodeAsync(HttpContext h, Guid seasonId, Guid? groupId, CancellationToken ct) =>
        CharacterCommandAsync(h, seasonId, null, groupId.HasValue ? "discord.group.code" : "discord.player.code", async (season, actor) =>
        {
            ApiFault.Require(Configured && await db.DiscordPolicies.AnyAsync(x => x.CommunityId == seasonId && x.Enabled, ct),
                409, "discord_disabled", "Discord rewards are disabled for this season.");
            if (groupId is { } group) await OwnedGroupAsync(seasonId, group, actor, ct);
            var now = time.GetUtcNow();
            await db.DiscordLinkCodes.Where(x => x.ExpiresAt <= now
                || x.CommunityId == seasonId && x.CharacterId == actor && x.GroupId == groupId).ExecuteDeleteAsync(ct);
            var code = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            db.DiscordLinkCodes.Add(new DiscordLinkCodeRow
            {
                Hash = SecretVault.Hash(code), CommunityId = seasonId, CharacterId = actor, GroupId = groupId, ExpiresAt = now.AddMinutes(10)
            });
            return new DiscordCode(code, now.AddMinutes(10));
        }, ct);

    private Task<IResult> UnlinkPlayerAsync(HttpContext h, Guid seasonId, CancellationToken ct) =>
        CharacterCommandAsync(h, seasonId, null, "discord.player.unlink", async (season, actor) =>
        {
            await db.DiscordPlayers.Where(x => x.CommunityId == seasonId && x.CharacterId == actor).ExecuteDeleteAsync(ct);
            await db.DiscordLinkCodes.Where(x => x.CommunityId == seasonId && x.CharacterId == actor && x.GroupId == null).ExecuteDeleteAsync(ct);
            return false;
        }, ct);

    private Task<IResult> UnlinkGroupAsync(HttpContext h, Guid seasonId, Guid groupId, CancellationToken ct) =>
        CharacterCommandAsync(h, seasonId, null, "discord.group.unlink", async (season, actor) =>
        {
            await OwnedGroupAsync(seasonId, groupId, actor, ct);
            await DisconnectGroupAsync(db, seasonId, groupId, ct);
            return false;
        }, ct);

    internal static async Task DisconnectGroupAsync(AmorDbContext db, Guid season, Guid group, CancellationToken ct)
    {
        await db.DiscordLinkCodes.Where(x => x.CommunityId == season && x.GroupId == group).ExecuteDeleteAsync(ct);
        await db.DiscordGuilds.Where(x => x.CommunityId == season && x.GroupId == group && x.Active).ExecuteUpdateAsync(
            fields => fields.SetProperty(x => x.Active, false).SetProperty(x => x.LeaveRequested, true), ct);
    }

    internal static async Task DisconnectSeasonAsync(AmorDbContext db, Guid season, CancellationToken ct)
    {
        await db.DiscordLinkCodes.Where(x => x.CommunityId == season).ExecuteDeleteAsync(ct);
        await db.DiscordGuilds.Where(x => x.CommunityId == season && x.Active).ExecuteUpdateAsync(
            fields => fields.SetProperty(x => x.Active, false).SetProperty(x => x.LeaveRequested, true), ct);
    }

    private async Task<GroupRow> OwnedGroupAsync(Guid season, Guid groupId, Guid actor, CancellationToken ct)
    {
        var group = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == season && x.Id == groupId && !x.Deleted, ct);
        ApiFault.Require(group != null && group.OwnerCharacterId == actor && group.EnabledFeatures.Contains(GroupCapabilities.Discord),
            403, "discord_group_permission", $"Only the group owner with {GuildAccess} may connect this group.");
        return group!;
    }

    private async Task<IResult> CharacterCommandAsync<T>(HttpContext h, Guid seasonId, object? body, string kind,
        Func<CommunityRow, Guid, Task<T>> action, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        var scope = "character:" + actor;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [scope, LinkLock, "season:" + seasonId], ct);
        var (season, _) = await seasons.RequireAsync(seasonId, actor, ct);
        var replay = await commands.ReplayAsync(h, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        var op = commands.New(h, scope, body, actor, seasonId, kind);
        // Pairing codes expire and are single-use. They are encrypted in the normal
        // command replay store and are never included in operation summaries.
        var result = await commands.SaveAsync(h, op, new CommandResult<T>(op.Id, await action(season, actor)), ct);
        await tx.CommitAsync(ct);
        return result;
    }
}
