using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Infrastructure.Persistence;

public sealed class DiscordPolicyRow
{
    public Guid CommunityId { get; set; }
    public bool Enabled { get; set; }
    public int CoinsPerPost { get; set; }
    public int MinimumCharacters { get; set; }
    public int CooldownSeconds { get; set; }
    public int WeeklyLimit { get; set; }
    public int SeasonWeeklyBudget { get; set; }
    public long BalanceCeiling { get; set; }
}

public sealed class DiscordLinkCodeRow
{
    public string Hash { get; set; } = "";
    public Guid CommunityId { get; set; }
    public Guid CharacterId { get; set; }
    public Guid? GroupId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class DiscordGuildRow
{
    public string GuildId { get; set; } = "";
    public Guid CommunityId { get; set; }
    public Guid GroupId { get; set; }
    public bool Active { get; set; }
    public bool LeaveRequested { get; set; }
    public string[] Channels { get; set; } = [];
    public string? Role { get; set; }
    public bool Paused { get; set; }
}

public sealed class DiscordPlayerRow
{
    public Guid CommunityId { get; set; }
    public string UserId { get; set; } = "";
    public Guid CharacterId { get; set; }
}

public sealed class DiscordAwardRow
{
    public string MessageId { get; set; } = "";
    public Guid CommunityId { get; set; }
    public string UserId { get; set; } = "";
    public Guid CharacterId { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public int Amount { get; set; }
}

public sealed partial class AmorDbContext
{
    public DbSet<DiscordPolicyRow> DiscordPolicies => Set<DiscordPolicyRow>();
    public DbSet<DiscordLinkCodeRow> DiscordLinkCodes => Set<DiscordLinkCodeRow>();
    public DbSet<DiscordGuildRow> DiscordGuilds => Set<DiscordGuildRow>();
    public DbSet<DiscordPlayerRow> DiscordPlayers => Set<DiscordPlayerRow>();
    public DbSet<DiscordAwardRow> DiscordAwards => Set<DiscordAwardRow>();

    private static void ConfigureDiscord(ModelBuilder model)
    {
        model.Entity<DiscordPolicyRow>(e =>
        {
            e.ToTable("discord_reward_policies");
            e.HasKey(x => x.CommunityId);
            e.HasOne<CommunityRow>().WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DiscordLinkCodeRow>(e =>
        {
            e.ToTable("discord_link_codes");
            e.HasKey(x => x.Hash);
            e.HasIndex(x => new { x.CommunityId, x.CharacterId, x.GroupId });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DiscordGuildRow>(e =>
        {
            e.ToTable("discord_guilds");
            e.HasKey(x => x.GuildId);
            e.HasIndex(x => new { x.CommunityId, x.GroupId }).IsUnique().HasFilter("\"Active\" = true");
            e.HasOne<GroupRow>().WithMany().HasForeignKey(x => new { x.CommunityId, Id = x.GroupId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DiscordPlayerRow>(e =>
        {
            e.ToTable("discord_players");
            e.HasKey(x => new { x.CommunityId, x.UserId });
            e.HasIndex(x => new { x.CommunityId, x.CharacterId }).IsUnique();
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DiscordAwardRow>(e =>
        {
            e.ToTable("discord_awards", t => t.HasCheckConstraint("ck_discord_award_positive", "\"Amount\" > 0"));
            e.HasKey(x => x.MessageId);
            e.HasIndex(x => new { x.CommunityId, x.OccurredAt });
            e.HasIndex(x => new { x.CommunityId, x.UserId, x.OccurredAt });
            e.HasIndex(x => new { x.CommunityId, x.CharacterId, x.OccurredAt });
            e.HasOne<MembershipRow>().WithMany().HasForeignKey(x => new { x.CommunityId, x.CharacterId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
