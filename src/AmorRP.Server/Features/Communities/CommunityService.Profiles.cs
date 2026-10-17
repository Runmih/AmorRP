using AmorRP.Contracts.Common;
using AmorRP.Contracts.Communities;
using AmorRP.Core.Items;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;

namespace AmorRP.Server.Features.Communities;

public sealed partial class CommunityService
{
    public async Task<IResult> MemberDescriptionAsync(HttpContext http, Guid communityId, Guid characterId, MemberDescriptionEdit body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(http, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var (community, member) = await access.RequireAsync(communityId, actor, ct);
        ApiFault.Require(actor == characterId, 403, "description_self_only", "Only this character can edit their description.");
        var error = PlainText.Error(body.Description, MemberDescriptionEdit.MaximumCharacters, multiline: true, required: false);
        ApiFault.Require(error == null, 422, "invalid_description", error ?? "Invalid description.");
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        CommandStore.Match(http, CommunityAccess.MemberETag(member));
        member.Description = body.Description; member.Version++; community.Version++;
        var op = commands.New(http, scope, body, actor, communityId, "member.description");
        op.SubjectCharacterId = actor; op.Summary = "Character description updated.";
        var result = await MemberViewAsync(community, member, ct);
        http.Response.Headers.ETag = result.Etag;
        var response = await commands.SaveAsync(http, op, new CommandResult<Member>(op.Id, result), ct);
        await tx.CommitAsync(ct); return response;
    }
}
