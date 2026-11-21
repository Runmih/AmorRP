using System.Globalization;
using System.Text.Json;
using AmorRP.Contracts.Common;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Server.Features.Authentication;
using AmorRP.Server.Features.Seasons;
using AmorRP.Server.Features.Currency;
using AmorRP.Server.Features.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Billing;

public sealed partial class BillingService(AmorDbContext db, SessionAccess sessions, SeasonAccess communities, CommandStore commands, DebtSettlement settlement, PageCursor pages, TimeProvider time)
{
    private static string Units(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static Bill View(BillRow b) => new(b.Id, b.CommunityId, b.GroupId, b.GroupName, b.IssuerCharacterId, b.IssuerName,
        b.CharacterId, b.CharacterName, b.Service, Units(b.Amount), Units(b.Collected), Units(b.Amount - b.Collected),
        b.CreatedAt, b.TaxPercent, b.CommissionPercent, BillingSplit.Total(b.Collected, b.TaxPercent, b.CommissionPercent).View());
    private static BillingPolicy ViewPolicy(CommunityRow season, Guid actor) =>
        new(season.AllowDebt, season.BillingPolicyVersion, CommandStore.ETag("billing-policy", season.Id, season.BillingPolicyVersion),
            season.BillingTaxPercent, season.OwnerCharacterId == actor ? Units(season.SeasonTreasuryOwned) : null);
    private async Task<GroupRow> GroupAsync(Guid communityId, Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == groupId, ct);
        ApiFault.Require(group != null, 404, "not_found", "Group not found in this season."); return group!;
    }
    public async Task<IResult> CreateAsync(HttpContext h, Guid communityId, Guid groupId, BillCreate body, CancellationToken ct)
    {
        ApiFault.Require(body.BillId.Version == 7 && WholeUnits.TryNormalize(body.Amount, out _, out var _) && PlainText.Error(body.Service, 100) == null,
            422, "invalid_bill", "Choose a customer, positive whole-unit amount and service of up to 100 characters.");
        WholeUnits.TryNormalize(body.Amount, out _, out var amount); ApiFault.Require(amount > 0, 422, "invalid_bill", "Enter a positive whole number of coins.");
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        var (community, _) = await communities.RequireAsync(communityId, actor, ct);
        var group = await GroupAsync(communityId, groupId, ct);
        ApiFault.Require(!group.Deleted, 404, "not_found", "This group has disbanded.");
        var staff = await GroupAuthorization.ActionAsync(db, group, actor, GroupCapabilities.Billing, ct);
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var hash = SecretVault.Hash(JsonSerializer.Serialize(new { body.CharacterId, amount, service = body.Service.Trim() }, CommandStore.Json));
        var bill = await db.Bills.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.Id == body.BillId, ct);
        var recovering = bill != null;
        if (bill != null)
            ApiFault.Require(bill.GroupId == groupId && bill.IssuerCharacterId == actor && bill.RequestHash == hash, 409, "bill_identity_mismatch", "That bill ID already belongs to a different charge. Refresh its receipt.");
        else
        {
            ApiFault.Require(!group.RequireClockIn || staff.ClockedInAt != null, 403, "billing_clock_in_required", "Clock in before charging customers.");
            var payer = await communities.TargetAsync(communityId, body.CharacterId, true, ct);
            ApiFault.Require(await db.GroupTrusts.AnyAsync(x => x.CommunityId == communityId && x.GroupId == groupId && x.CharacterId == body.CharacterId && x.Trusted, ct),
                403, "billing_not_trusted", "The customer must enable Trusted for automatic billing on this group's profile.");
            var cutoff = time.GetUtcNow().AddSeconds(-30);
            ApiFault.Require(!await db.Bills.AnyAsync(x => x.CommunityId == communityId && x.GroupId == groupId && x.IssuerCharacterId == actor && x.CharacterId == body.CharacterId && x.CreatedAt > cutoff, ct),
                409, "billing_cooldown", "Wait 30 seconds before charging this customer again. The previous bill is in the records.");
            await settlement.CollectAsync(payer, "billing.prepare", body.BillId, ct);
            ApiFault.Require(community.AllowDebt || CurrencyAccounts.Available(payer) >= amount, 409, "billing_insufficient_funds", "The customer cannot afford this bill. No coins were charged; this season does not allow new debt.");
            var paidNow = Math.Min(CurrencyAccounts.Available(payer), amount);
            var characters = await db.Characters.Where(x => x.Id == actor || x.Id == body.CharacterId).ToDictionaryAsync(x => x.Id, ct);
            bill = new BillRow { Id = body.BillId, CommunityId = communityId, GroupId = groupId, GroupName = group.Name,
                IssuerCharacterId = actor, IssuerName = characters[actor].DisplayName, CharacterId = body.CharacterId, CharacterName = characters[body.CharacterId].DisplayName,
                Service = body.Service.Trim(), Amount = amount, RequestHash = hash, CreatedAt = time.GetUtcNow(),
                TaxPercent = community.BillingTaxPercent, CommissionPercent = group.CommissionPercent };
            await settlement.RequireCapacityAsync(bill, payer, group, paidNow, ct);
            db.Bills.Add(bill); await settlement.CollectAsync(payer, "bill.create", bill.Id, ct);
        }
        var op = commands.New(h, scope, body, actor, communityId, recovering ? "bill.recover" : "bill.create"); op.SubjectCharacterId = bill!.CharacterId;
        op.Summary = recovering ? "Existing bill retrieved; no new charge." : $"Bill: {bill.Service}; {bill.Collected} collected, {bill.Amount - bill.Collected} still owed.";
        var result = await commands.SaveAsync(h, op, new CommandResult<Bill>(op.Id, View(bill)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<IResult> PolicyAsync(HttpContext h, Guid communityId, BillingPolicyEdit? body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        if (body == null)
        {
            var (community, _) = await communities.RequireAsync(communityId, actor, ct);
            return Results.Json(ViewPolicy(community, actor));
        }
        await using var tx = await db.Database.BeginTransactionAsync(ct); await CommandStore.LockAsync(db, ["character:" + actor, "season:" + communityId], ct);
        var (target, _) = await communities.RequireAsync(communityId, actor, ct); SeasonAccess.Owner(target, actor);
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("billing-policy", communityId, target.BillingPolicyVersion));
        ApiFault.Require(body.TaxPercent is null or >= 0 and <= 100, 422, "invalid_tax_rate", "Tax must be a whole percentage from 0 to 100.");
        target.AllowDebt = body.AllowDebt;
        if (body.TaxPercent.HasValue) target.BillingTaxPercent = body.TaxPercent.Value;
        target.BillingPolicyVersion++;
        var dto = ViewPolicy(target, actor); h.Response.Headers.ETag = dto.Etag;
        var op = commands.New(h, scope, body, actor, communityId, "billing.policy");
        op.Summary = $"Billing settings saved: {target.BillingTaxPercent}% tax, new debt {(target.AllowDebt ? "enabled" : "disabled")}.";
        var result = await commands.SaveAsync(h, op, new CommandResult<BillingPolicy>(op.Id, dto), ct); await tx.CommitAsync(ct); return result;
    }
}
