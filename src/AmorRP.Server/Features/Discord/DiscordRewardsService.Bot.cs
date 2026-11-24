using AmorRP.Contracts.Common;
using AmorRP.Contracts.Discord;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Discord;

public sealed partial class DiscordRewardsService
{
    private async Task<IResult> RedeemAsync(HttpContext h, DiscordRedeem body, bool groupCode, CancellationToken ct)
    {
        RequireBot(h);
        ApiFault.Require(body.Code is { Length: 43 } && IsSnowflake(body.GuildId) && IsSnowflake(body.UserId),
            422, "invalid_discord_input", "Invalid Discord link.");
        var hash = SecretVault.Hash(body.Code);
        var seasonId = await db.DiscordLinkCodes.AsNoTracking().Where(x => x.Hash == hash)
            .Select(x => (Guid?)x.CommunityId).SingleOrDefaultAsync(ct);
        ApiFault.Require(seasonId != null, 404, "discord_code_expired", "Generate a new link code in Amor RP.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [LinkLock, "season:" + seasonId], ct);
        var code = await db.DiscordLinkCodes.FindAsync([hash], ct);
        ApiFault.Require(code != null && code.GroupId.HasValue == groupCode && code.ExpiresAt > time.GetUtcNow(),
            404, "discord_code_expired", "Generate a new link code in Amor RP.");
        var (season, _) = await seasons.RequireAsync(seasonId!.Value, code!.CharacterId, ct);
        ApiFault.Require(season.SeasonState == ActiveSeason
            && await db.DiscordPolicies.AnyAsync(x => x.CommunityId == season.Id && x.Enabled, ct),
            409, "discord_disabled", "Discord rewards are disabled for this season.");
        var guild = await db.DiscordGuilds.FindAsync([body.GuildId], ct);
        if (code.GroupId is { } group)
        {
            await OwnedGroupAsync(season.Id, group, code.CharacterId, ct);
            ApiFault.Require(guild?.Active != true && guild?.LeaveRequested != true
                && !await db.DiscordGuilds.AnyAsync(x => x.CommunityId == season.Id && x.GroupId == group && x.Active, ct),
                409, "discord_already_connected", "Disconnect the existing server and let the bot leave before reconnecting.");
            if (guild == null)
            {
                guild = new DiscordGuildRow { GuildId = body.GuildId };
                db.DiscordGuilds.Add(guild);
            }
            guild.CommunityId = season.Id;
            guild.GroupId = group;
            guild.Active = true;
            guild.LeaveRequested = false;
            guild.Channels = [];
            guild.Role = null;
            guild.Paused = false;
        }
        else
        {
            ApiFault.Require(guild?.Active == true && guild.CommunityId == season.Id && await GuildAuthorizedAsync(guild, ct),
                403, "discord_guild_required", "Use a connected Discord server for this season.");
            var player = await db.DiscordPlayers.FindAsync([season.Id, body.UserId], ct);
            ApiFault.Require(!await db.DiscordPlayers.AnyAsync(x => x.CommunityId == season.Id
                && x.CharacterId == code.CharacterId && x.UserId != body.UserId, ct),
                409, "discord_character_linked", "Unlink this character in Amor RP before changing Discord accounts.");
            if (player == null)
            {
                player = new() { CommunityId = season.Id, UserId = body.UserId };
                db.DiscordPlayers.Add(player);
            }
            player.CharacterId = code.CharacterId;
        }
        db.DiscordLinkCodes.Remove(code);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok();
    }

    private Task<bool> GuildAuthorizedAsync(DiscordGuildRow guild, CancellationToken ct) =>
        db.Groups.AnyAsync(x => x.CommunityId == guild.CommunityId && x.Id == guild.GroupId && !x.Deleted
            && x.EnabledFeatures.Contains(GroupCapabilities.Discord), ct);

    private async Task<IResult> ConfigureGuildAsync(HttpContext h, string guildId, DiscordChannels body, CancellationToken ct)
    {
        RequireBot(h);
        ApiFault.Require(IsSnowflake(guildId) && body.ChannelIds is { Length: <= 25 }
            && body.ChannelIds.All(IsSnowflake) && (body.RequiredRoleId == null || IsSnowflake(body.RequiredRoleId)),
            422, "invalid_discord_input", "Choose up to 25 channels and an optional Discord role.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [LinkLock], ct);
        var guild = await db.DiscordGuilds.FindAsync([guildId], ct);
        ApiFault.Require(guild?.Active == true && await GuildAuthorizedAsync(guild, ct),
            404, "discord_guild_required", "Connect this server first.");
        guild!.Channels = body.ChannelIds.Distinct().ToArray();
        guild.Role = body.RequiredRoleId;
        guild.Paused = body.Paused;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok();
    }

    private async Task<IResult> GuildsAsync(HttpContext h, CancellationToken ct)
    {
        RequireBot(h);
        var active = await db.DiscordGuilds.Where(x => x.Active).Select(x => x.GuildId).ToArrayAsync(ct);
        var leave = await db.DiscordGuilds.Where(x => x.LeaveRequested).Select(x => x.GuildId).ToArrayAsync(ct);
        return Results.Json(new DiscordGuilds(active, leave));
    }

    private async Task<IResult> RemovedAsync(HttpContext h, string guildId, CancellationToken ct)
    {
        RequireBot(h);
        ApiFault.Require(IsSnowflake(guildId), 422, "invalid_discord_input", "Invalid Discord server.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [LinkLock], ct);
        await db.DiscordGuilds.Where(x => x.GuildId == guildId).ExecuteUpdateAsync(
            fields => fields.SetProperty(x => x.LeaveRequested, false).SetProperty(x => x.Active, false), ct);
        await tx.CommitAsync(ct);
        return Results.Ok();
    }

    private async Task<IResult> PostAsync(HttpContext h, DiscordPost body, CancellationToken ct)
    {
        RequireBot(h);
        ApiFault.Require(IsSnowflake(body.MessageId) && IsSnowflake(body.GuildId) && IsSnowflake(body.ChannelId)
            && IsSnowflake(body.UserId) && body.RoleIds is { Length: <= 250 } && body.RoleIds.All(IsSnowflake)
            && body.Fingerprint is { Length: 64 } && body.Fingerprint.All(char.IsAsciiHexDigit)
            && body.Characters is >= 0 and <= 4000, 422, "invalid_discord_input", "Invalid Discord activity.");
        var now = time.GetUtcNow();
        if (body.CreatedAt < now.AddMinutes(-5) || body.CreatedAt > now.AddSeconds(15)) return Results.Json(new DiscordAward(0));
        var seasonId = await db.DiscordGuilds.AsNoTracking().Where(x => x.GuildId == body.GuildId && x.Active)
            .Select(x => (Guid?)x.CommunityId).SingleOrDefaultAsync(ct);
        if (seasonId == null) return Results.Json(new DiscordAward(0));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, [LinkLock, "season:" + seasonId], ct);

        var guild = await db.DiscordGuilds.FindAsync([body.GuildId], ct);
        if (guild?.Active != true || guild.CommunityId != seasonId || guild.Paused || !guild.Channels.Contains(body.ChannelId)
            || guild.Role != null && !body.RoleIds.Contains(guild.Role) || !await GuildAuthorizedAsync(guild, ct))
            return Results.Json(new DiscordAward(0));
        var policy = await db.DiscordPolicies.FindAsync([seasonId.Value], ct);
        if (policy?.Enabled != true || body.Characters < policy.MinimumCharacters) return Results.Json(new DiscordAward(0));
        var season = await db.Seasons.SingleOrDefaultAsync(x => x.Id == seasonId && !x.Deleted && x.SeasonState == ActiveSeason, ct);
        if (season == null) return Results.Json(new DiscordAward(0));
        var player = await db.DiscordPlayers.FindAsync([season.Id, body.UserId], ct);
        if (player == null) return Results.Json(new DiscordAward(0));
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.CommunityId == season.Id
            && x.CharacterId == player.CharacterId && x.Status == "active", ct);
        if (member == null || member.Owned >= policy.BalanceCeiling
            || await db.DiscordAwards.AnyAsync(x => x.MessageId == body.MessageId, ct)) return Results.Json(new DiscordAward(0));

        var week = SeasonAccess.Period(now);
        var awards = db.DiscordAwards.Where(x => x.CommunityId == season.Id && x.OccurredAt >= week);
        var personal = awards.Where(x => x.UserId == player.UserId || x.CharacterId == member.CharacterId);
        var cooldown = now.AddSeconds(-policy.CooldownSeconds);
        var fingerprint = body.Fingerprint.ToLowerInvariant();
        if (await personal.AnyAsync(x => x.OccurredAt > cooldown || x.Fingerprint == fingerprint, ct))
            return Results.Json(new DiscordAward(0));
        var personalTotal = await personal.SumAsync(x => (long)x.Amount, ct);
        var seasonTotal = await awards.SumAsync(x => (long)x.Amount, ct);
        var amount = (int)Math.Min(Math.Min(policy.CoinsPerPost, policy.BalanceCeiling - member.Owned),
            Math.Min(policy.WeeklyLimit - personalTotal, policy.SeasonWeeklyBudget - seasonTotal));
        if (amount <= 0) return Results.Json(new DiscordAward(0));

        var operation = new OperationRow { Scope = "discord:awards", Key = Guid.CreateVersion7(now),
            RequestHash = SecretVault.Hash(body.MessageId), CommunityId = season.Id, ActorCharacterId = member.CharacterId, SubjectCharacterId = member.CharacterId,
            Kind = "discord.award", Summary = $"Earned {amount} coins for Discord writing.", Delta = amount, OccurredAt = now,
            BeforeBalance = member.Owned };
        CurrencyAccounts.Apply(member, amount);
        operation.AfterBalance = member.Owned;
        db.Operations.Add(operation);
        db.DiscordAwards.Add(new DiscordAwardRow { MessageId = body.MessageId, CommunityId = season.Id,
            UserId = player.UserId, CharacterId = member.CharacterId, OccurredAt = now, Fingerprint = fingerprint, Amount = amount });
        await settlement.CollectAsync(member, "discord.award", operation.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Json(new DiscordAward(amount));
    }
}
