using AmorRP.Contracts.Auth;
using AmorRP.Core.Items;
using AmorRP.Contracts.Seasons;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Seasons;

public sealed class SeasonAccess(AmorDbContext db)
{
    public static readonly string[] GrantableActions = [CraftingTypes.BlacksmithPermission, CraftingTypes.BlacksmithRecipePermission, "currency.manage", "items.potion.create", "inventory.remove", "items.potion.define", "items.cooking.create", "items.cooking.define", "season.moderate", "announcements.manage", "members.gameplay.manage", "groups.create", "groups.manage", AmorRP.Server.Features.Permits.PermitService.ManageListings, AmorRP.Core.Items.CraftingTypes.GatherPermission];
    public async Task<(CommunityRow Season, MembershipRow Member)> RequireAsync(Guid communityId, Guid actor, CancellationToken ct)
    {
        var community = await db.Seasons.SingleOrDefaultAsync(x => x.Id == communityId && !x.Deleted, ct);
        if (community != null) await SeasonMemberships.EnrollAsync(db, actor, communityId, ct);
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.CharacterId == actor && x.Status == "active", ct);
        ApiFault.Require(community != null && member != null, 404, "not_found", "Season was not found or is inaccessible.");
        return (community!, member!);
    }
    public static void Owner(CommunityRow community, Guid actor) => ApiFault.Require(community.OwnerCharacterId == actor, 403, "owner_required", "Only the season owner may do this.");
    public static bool HasAction(CommunityRow season, MembershipRow member, string capability) =>
        season.OwnerCharacterId == member.CharacterId ||
        (!member.BlockedGameplayGrants.Contains(capability) &&
            (member.Capabilities.Contains(capability) || member.ClassCapabilities.Contains(capability)));
    public static void Action(CommunityRow community, MembershipRow member, string capability) =>
        ApiFault.Require(HasAction(community, member, capability), 403, "capability_required", "This action requires an individual authorization.");
    public async Task<MembershipRow> TargetAsync(Guid communityId, Guid target, bool activeOnly, CancellationToken ct)
    {
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.CharacterId == target, ct);
        ApiFault.Require(member != null && (!activeOnly || member.Status == "active"), 404, "not_found", "Season member not found.");
        return member!;
    }
    public static AmorRP.Contracts.Currency.Currency Currency(CommunityRow community) => new(community.CurrencyId, community.Id, community.CurrencyName, community.CurrencySymbol, community.CurrencyVersion, community.CurrencyIconAssetId);
    public static Balance Balance(CommunityRow community, MembershipRow member) => new(community.CurrencyId, member.CharacterId,
        member.Owned.ToString(System.Globalization.CultureInfo.InvariantCulture), member.Reserved.ToString(System.Globalization.CultureInfo.InvariantCulture),
        AmorRP.Server.Features.Currency.CurrencyAccounts.Available(member).ToString(System.Globalization.CultureInfo.InvariantCulture), member.BalanceVersion,
        (member.CollectionPending ? member.Owned - member.Reserved : 0).ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static string MemberETag(MembershipRow member) => CommandStore.ETag("member-" + member.CommunityId.ToString("N"), member.CharacterId, member.Version);
    public async Task<Season> ViewAsync(CommunityRow community, MembershipRow member, DateTimeOffset now, CancellationToken ct)
    {
        var transfer = await db.OwnershipTransfers.Where(x => x.CommunityId == community.Id && x.Status == "proposed" && x.ExpiresAt > now).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return new(community.Id, community.Name, community.Description, community.OwnerCharacterId, community.Version, Currency(community),
            community.OwnerCharacterId == member.CharacterId ? GrantableActions : member.Capabilities.Union(member.ClassCapabilities).Except(member.BlockedGameplayGrants).ToArray(), member.TradingRestricted, Balance(community, member), transfer, community.LogoAssetId, community.LogoVersion, community.SeasonState);
    }
    public static DateTimeOffset Period(DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }
    public static SeasonPolicies Policies(CommunityRow community, DateTimeOffset now)
    {
        var period = Period(now);
        var promoted = period > community.PolicyPeriod;
        return new(community.Id, community.PolicyVersion, period, period.AddDays(7),
            new(promoted ? community.NextPotionPoints : community.CurrentPotionPoints, promoted ? community.NextLetters : community.CurrentLetters, promoted ? community.NextCookingPoints : community.CurrentCookingPoints),
            new(community.NextPotionPoints, community.NextLetters, community.NextCookingPoints));
    }
    public static int GatheringPoints(CommunityRow season, DateTimeOffset now) =>
        Period(now) > season.PolicyPeriod ? season.NextGatheringPoints : season.CurrentGatheringPoints;
    public static void PromotePolicy(CommunityRow community, DateTimeOffset now)
    {
        var period = Period(now);
        if (period <= community.PolicyPeriod) return;
        community.CurrentGatheringPoints = community.NextGatheringPoints;
        community.CurrentCookingPoints = community.NextCookingPoints; community.CurrentPotionPoints = community.NextPotionPoints; community.CurrentLetters = community.NextLetters; community.PolicyPeriod = period;
    }
}
