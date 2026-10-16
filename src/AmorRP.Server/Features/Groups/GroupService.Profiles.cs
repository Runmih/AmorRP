using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Core.Items;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;

namespace AmorRP.Server.Features.Groups;

public sealed partial class GroupService
{
    public async Task<IResult> MemberDescriptionAsync(HttpContext http, Guid groupId, Guid characterId, MemberDescriptionEdit body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(http, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "group:" + groupId], ct);
        var (group, member) = await access.RequireAsync(groupId, actor, ct);
        ApiFault.Require(actor == characterId, 403, "description_self_only", "Only this character can edit their description.");
        var error = PlainText.Error(body.Description, MemberDescriptionEdit.MaximumCharacters, multiline: true, required: false);
        ApiFault.Require(error == null, 422, "invalid_description", error ?? "Invalid description.");
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(http, scope, body, ct);
        if (replay != null) return commands.Replay(replay);
        CommandStore.Match(http, GroupAccess.MemberETag(member));
        member.Description = body.Description; member.Version++; group.Version++;
        var op = commands.New(http, scope, body, actor, groupId, "member.description");
        op.SubjectCharacterId = actor; op.Summary = "Character description updated.";
        var result = await MemberViewAsync(group, member, ct);
        http.Response.Headers.ETag = result.Etag;
        var response = await commands.SaveAsync(http, op, new CommandResult<Member>(op.Id, result), ct);
        await tx.CommitAsync(ct); return response;
    }
}
