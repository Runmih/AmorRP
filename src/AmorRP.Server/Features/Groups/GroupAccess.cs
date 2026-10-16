using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Groups;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.History;
using AmorRP.Contracts.Common;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Groups;

public sealed class GroupAccess(AmorDbContext db)
{
    public static readonly string[] GrantableActions = ["currency.manage", "items.potion.create", "inventory.remove", "items.potion.define", "group.moderate", "announcements.manage", "members.gameplay.manage"];
    public async Task<(GroupRow Group, MembershipRow Member)> RequireAsync(Guid groupId, Guid actor, CancellationToken ct)
    {
        var group = await db.Groups.SingleOrDefaultAsync(x => x.Id == groupId && !x.Deleted, ct);
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.GroupId == groupId && x.CharacterId == actor && x.Status == "active", ct);
        ApiFault.Require(group != null && member != null, 404, "not_found", "Group was not found or is inaccessible.");
        return (group!, member!);
    }
    public static void Owner(GroupRow group, Guid actor) => ApiFault.Require(group.OwnerCharacterId == actor, 403, "owner_required", "Only the group owner may do this.");
    public static void Action(GroupRow group, MembershipRow member, string capability) =>
        ApiFault.Require(group.OwnerCharacterId == member.CharacterId || member.Capabilities.Contains(capability), 403, "capability_required", "This action requires an individual authorization.");
    public async Task<MembershipRow> TargetAsync(Guid groupId, Guid target, bool activeOnly, CancellationToken ct)
    {
        var member = await db.Memberships.SingleOrDefaultAsync(x => x.GroupId == groupId && x.CharacterId == target, ct);
        ApiFault.Require(member != null && (!activeOnly || member.Status == "active"), 404, "not_found", "Group member not found.");
        return member!;
    }
    public static AmorRP.Contracts.Currency.Currency Currency(GroupRow group) => new(group.CurrencyId, group.Id, group.CurrencyName, group.CurrencySymbol, group.CurrencyVersion, group.CurrencyIconAssetId);
    public static Balance Balance(GroupRow group, MembershipRow member) => new(group.CurrencyId, member.CharacterId,
        member.Owned.ToString(System.Globalization.CultureInfo.InvariantCulture), member.Reserved.ToString(System.Globalization.CultureInfo.InvariantCulture),
        (member.Owned - member.Reserved).ToString(System.Globalization.CultureInfo.InvariantCulture), member.BalanceVersion);
    public static string MemberETag(MembershipRow member) => CommandStore.ETag("member-" + member.GroupId.ToString("N"), member.CharacterId, member.Version);
    public async Task<Group> ViewAsync(GroupRow group, MembershipRow member, DateTimeOffset now, CancellationToken ct)
    {
        var transfer = await db.OwnershipTransfers.Where(x => x.GroupId == group.Id && x.Status == "proposed" && x.ExpiresAt > now).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return new(group.Id, group.Name, group.Description, group.OwnerCharacterId, group.Version, Currency(group),
            group.OwnerCharacterId == member.CharacterId ? GrantableActions : member.Capabilities, member.TradingRestricted, Balance(group, member), transfer, group.LogoAssetId, group.LogoVersion);
    }
    public static DateTimeOffset Period(DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }
    public static GroupPolicies Policies(GroupRow group, DateTimeOffset now)
    {
        var period = Period(now);
        var promoted = period > group.PolicyPeriod;
        return new(group.Id, group.PolicyVersion, period, period.AddDays(7),
            new(promoted ? group.NextPotionPoints : group.CurrentPotionPoints, promoted ? group.NextLetters : group.CurrentLetters),
            new(group.NextPotionPoints, group.NextLetters));
    }
    public static void PromotePolicy(GroupRow group, DateTimeOffset now)
    {
        var period = Period(now);
        if (period <= group.PolicyPeriod) return;
        group.CurrentPotionPoints = group.NextPotionPoints; group.CurrentLetters = group.NextLetters; group.PolicyPeriod = period;
    }
}
