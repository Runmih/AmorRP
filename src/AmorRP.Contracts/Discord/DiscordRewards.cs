namespace AmorRP.Contracts.Discord;

public sealed record DiscordRewardPolicy(bool Enabled = false, int CoinsPerPost = 10, int MinimumCharacters = 40,
    int CooldownSeconds = 60, int WeeklyLimit = 500, int SeasonWeeklyBudget = 10000, long BalanceCeiling = 10000);
public sealed record DiscordPlayerState(bool Available, bool Linked, DiscordRewardPolicy? Policy);
public sealed record DiscordGroupState(bool Connected, string? GuildId);
public sealed record DiscordCode(string Code, DateTimeOffset ExpiresAt);
public sealed record DiscordRedeem(string Code, string GuildId, string UserId);
public sealed record DiscordChannels(string[] ChannelIds, string? RequiredRoleId, bool Paused);
public sealed record DiscordPost(string MessageId, string GuildId, string ChannelId, string UserId,
    string[] RoleIds, int Characters, string Fingerprint, DateTimeOffset CreatedAt);
public sealed record DiscordAward(int Amount);
public sealed record DiscordGuilds(string[] Connected, string[] Leave);
