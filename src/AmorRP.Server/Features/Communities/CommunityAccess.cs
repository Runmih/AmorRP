using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Communities;

public sealed class CommunityAccess(AmorDbContext db)
{
    public static readonly string[] GrantableActions = ["currency.manage", "items.potion.create", "inventory.remove", "items.potion.define", "community.moderate", "announcements.manage", "members.gameplay.manage", "groups.create", "groups.manage"];
    public async Task<(CommunityRow Community, MembershipRow Member)> RequireAsync(Guid communityId, Guid actor, CancellationToken ct)
    {
        var community = await db.Communities.SingleOrDefaultAsync(x => x.Id == communityId && !x.Deleted, ct);
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.CharacterId == actor && x.Status == "active", ct);
        ApiFault.Require(community != null && member != null, 404, "not_found", "Community was not found or is inaccessible.");
        return (community!, member!);
    }
    public static void Owner(CommunityRow community, Guid actor) => ApiFault.Require(community.OwnerCharacterId == actor, 403, "owner_required", "Only the community owner may do this.");
    public static void Action(CommunityRow community, MembershipRow member, string capability) =>
        ApiFault.Require(community.OwnerCharacterId == member.CharacterId || member.Capabilities.Contains(capability), 403, "capability_required", "This action requires an individual authorization.");
    public async Task<MembershipRow> TargetAsync(Guid communityId, Guid target, bool activeOnly, CancellationToken ct)
    {
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.CharacterId == target, ct);
        ApiFault.Require(member != null && (!activeOnly || member.Status == "active"), 404, "not_found", "Community member not found.");
        return member!;
    }
    public static AmorRP.Contracts.Currency.Currency Currency(CommunityRow community) => new(community.CurrencyId, community.Id, community.CurrencyName, community.CurrencySymbol, community.CurrencyVersion, community.CurrencyIconAssetId);
    public static Balance Balance(CommunityRow community, MembershipRow member) => new(community.CurrencyId, member.CharacterId,
        member.Owned.ToString(System.Globalization.CultureInfo.InvariantCulture), member.Reserved.ToString(System.Globalization.CultureInfo.InvariantCulture),
        (member.Owned - member.Reserved).ToString(System.Globalization.CultureInfo.InvariantCulture), member.BalanceVersion);
    public static string MemberETag(MembershipRow member) => CommandStore.ETag("member-" + member.CommunityId.ToString("N"), member.CharacterId, member.Version);
    public async Task<Community> ViewAsync(CommunityRow community, MembershipRow member, DateTimeOffset now, CancellationToken ct)
    {
        var transfer = await db.OwnershipTransfers.Where(x => x.CommunityId == community.Id && x.Status == "proposed" && x.ExpiresAt > now).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return new(community.Id, community.Name, community.Description, community.OwnerCharacterId, community.Version, Currency(community),
            community.OwnerCharacterId == member.CharacterId ? GrantableActions : member.Capabilities, member.TradingRestricted, Balance(community, member), transfer, community.LogoAssetId, community.LogoVersion);
    }
    public static DateTimeOffset Period(DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }
    public static CommunityPolicies Policies(CommunityRow community, DateTimeOffset now)
    {
        var period = Period(now);
        var promoted = period > community.PolicyPeriod;
        return new(community.Id, community.PolicyVersion, period, period.AddDays(7),
            new(promoted ? community.NextPotionPoints : community.CurrentPotionPoints, promoted ? community.NextLetters : community.CurrentLetters),
            new(community.NextPotionPoints, community.NextLetters));
    }
    public static void PromotePolicy(CommunityRow community, DateTimeOffset now)
    {
        var period = Period(now);
        if (period <= community.PolicyPeriod) return;
        community.CurrentPotionPoints = community.NextPotionPoints; community.CurrentLetters = community.NextLetters; community.PolicyPeriod = period;
    }
}
