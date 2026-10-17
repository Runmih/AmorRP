using AmorRP.Contracts.Common;
using AmorRP.Contracts.Inventory;
using AmorRP.Core.Items;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Inventory;

public sealed partial class InventoryService
{
    private void ValidateLetter(string title, string body) => ApiFault.Require(ItemText.Plain(title, policy.Value.LetterTitleTextElements)
        && ItemText.Plain(body, policy.Value.LetterBodyTextElements, multiline: true, required: false), 422, "invalid_letter", "Letter exceeds the discovered limits or contains unsupported controls.");
    private async Task<Letter> LetterViewAsync(LetterRow row, HoldingRow holding, CancellationToken ct)
    {
        var author = await db.Characters.SingleAsync(x => x.Id == row.AuthorCharacterId, ct);
        return new(row.Id, row.CommunityId, new(author.Id, author.DisplayName, author.HomeWorldId, author.HomeWorldName, author.VerifiedAt, "verified"),
            row.CategoryId, row.Title, row.Body, row.Version, row.FirstTradedAt != null || holding.Reserved > 0, row.FirstTradedAt, row.TypeId);
    }
    public async Task<IResult> LetterAsync(HttpContext h, Guid communityId, Guid letterId, LetterEdit? edit, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = edit != null ? await db.Database.BeginTransactionAsync(ct) : null;
        if (edit != null) await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var (community, _) = await communities.RequireAsync(communityId, actor, ct);
        var holding = await db.Holdings.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.LetterId == letterId && x.CharacterId == actor && x.Quantity == 1, ct);
        ApiFault.Require(holding != null, 404, "not_found", "Letter not found or no longer owned.");
        var row = await db.Letters.SingleAsync(x => x.CommunityId == communityId && x.Id == letterId, ct);
        if (edit == null) { h.Response.Headers.ETag = CommandStore.ETag("letter", row.Id, row.Version); return Results.Json(await LetterViewAsync(row, holding!, ct)); }
        ApiFault.Require(row.AuthorCharacterId == actor && row.FirstTradedAt == null && holding!.Reserved == 0, 403, "letter_locked", "Only the author holding an untraded, unreserved letter may edit it.");
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, edit, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, CommandStore.ETag("letter", row.Id, row.Version)); ValidateLetter(edit.Title, edit.Body);
        row.Title = edit.Title.Trim(); row.Body = edit.Body; row.Version++; holding!.Name = row.Title; holding.Version++;
        community.InventoryVersion++;
        var op = commands.New(h, scope, edit, actor, communityId, "letter.edit"); op.SubjectCharacterId = actor; op.Summary = "Edited an untraded letter.";
        h.Response.Headers.ETag = CommandStore.ETag("letter", row.Id, row.Version);
        var result = await commands.SaveAsync(h, op, new CommandResult<Letter>(op.Id, await LetterViewAsync(row, holding, ct)), ct); await tx!.CommitAsync(ct); return result;
    }
}
