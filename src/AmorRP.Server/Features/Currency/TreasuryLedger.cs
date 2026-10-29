using System.Globalization;
using AmorRP.Contracts.Currency;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Currency;

public sealed class TreasuryLedger(AmorDbContext db, TimeProvider time)
{
    private static string Units(long amount) => amount.ToString(CultureInfo.InvariantCulture);
    public static TreasuryBalance Balance(GroupRow group, CommunityRow community) =>
        new(group.CommunityId, group.Id, community.CurrencyId, Units(group.TreasuryOwned), "0", Units(group.TreasuryOwned), group.TreasuryVersion);
    public static TreasuryReceipt View(TreasuryEntryRow e) => new(e.Id, e.CommunityId, e.GroupId, e.OperationId, e.ActorCharacterId, e.CharacterId,
        e.CharacterName, e.Kind, Units(e.Amount), Units(e.GroupBefore), Units(e.GroupAfter), Units(e.CharacterBefore), Units(e.CharacterAfter), e.Note, e.OccurredAt);

    // Both balances and the immutable receipt commit with the owning command.
    public async Task<TreasuryEntryRow> MoveAsync(GroupRow group, MembershipRow member, long amount, string kind, OperationRow operation, CancellationToken ct, string note = "")
    {
        ApiFault.Require(group.CommunityId == member.CommunityId && operation.CommunityId == group.CommunityId && amount > 0, 422, "invalid_quantity", "Enter a positive whole number of coins.");
        var deposit = kind == "deposit";
        long groupAfter;
        try { groupAfter = checked(group.TreasuryOwned + (deposit ? amount : -amount)); }
        catch (OverflowException) { throw new ApiFault(409, "balance_overflow", "The treasury cannot receive that many coins."); }
        ApiFault.Require(groupAfter >= 0, 409, "insufficient_available_quantity", "The treasury does not have enough coins.");
        var delta = deposit ? -amount : amount;
        var characterAfter = CurrencyAccounts.After(member, delta);
        var entry = new TreasuryEntryRow { CommunityId = group.CommunityId, GroupId = group.Id, OperationId = operation.Id,
            ActorCharacterId = operation.ActorCharacterId!.Value, CharacterId = member.CharacterId,
            CharacterName = await db.Characters.Where(x => x.Id == member.CharacterId).Select(x => x.DisplayName).SingleAsync(ct),
            Kind = kind, Amount = amount, GroupBefore = group.TreasuryOwned, GroupAfter = groupAfter,
            CharacterBefore = member.Owned, CharacterAfter = characterAfter, Note = note, OccurredAt = time.GetUtcNow() };
        group.TreasuryOwned = groupAfter; group.TreasuryVersion++; CurrencyAccounts.Apply(member, delta);
        db.TreasuryEntries.Add(entry); return entry;
    }

    public async Task<TreasuryEntryRow?> ReturnToOwnerAsync(GroupRow group, OperationRow operation, CancellationToken ct)
    {
        if (group.TreasuryOwned == 0) return null;
        // Dormant/blocked owners keep their original account; no moderator becomes the payee.
        var owner = await db.Memberships.SingleAsync(x => x.CommunityId == group.CommunityId && x.CharacterId == group.OwnerCharacterId, ct);
        var deliverable = Math.Min(group.TreasuryOwned, long.MaxValue - owner.Owned);
        // Any overflow remains on the retained deleted group for an explicit later collection.
        return deliverable == 0 ? null : await MoveAsync(group, owner, deliverable, "disband_return", operation, ct);
    }
}
