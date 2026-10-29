using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Features.Communities;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Groups;

public sealed partial class GroupService
{
    public async Task<IResult> FeaturesAsync(HttpContext h, Guid communityId, Guid groupId, GroupFeaturesEdit body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        var (community, _) = await communities.RequireAsync(communityId, actor, ct);
        CommunityAccess.Owner(community, actor);
        var group = await RequireAsync(communityId, groupId, ct);
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        CommandStore.Match(h, View(group).Etag); GroupAuthorization.Validate(body.EnabledFeatures);
        group.EnabledFeatures = body.EnabledFeatures.Order().ToArray(); group.Version++;
        foreach (var member in await db.GroupMemberships.Where(x => x.CommunityId == communityId && x.GroupId == groupId).ToArrayAsync(ct))
        {
            var retained = member.Capabilities.Intersect(group.EnabledFeatures).Order().ToArray();
            if (!member.Capabilities.Order().SequenceEqual(retained))
            { member.Capabilities = retained; member.PermissionVersion = Guid.CreateVersion7(); }
        }
        await LoadViewerAsync(actor, communityId, ct);
        var op = commands.New(h, scope, body, actor, communityId, "group.features");
        op.Summary = "Group features updated.";
        var dto = View(group); h.Response.Headers.ETag = dto.Etag;
        var result = await commands.SaveAsync(h, op, new CommandResult<Group>(op.Id, dto), ct);
        await tx.CommitAsync(ct); return result;
    }

    public async Task<IResult> StaffAsync(HttpContext h, Guid communityId, Guid groupId, Guid characterId, GroupStaffEdit body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        await communities.RequireAsync(communityId, actor, ct);
        var group = await RequireAsync(communityId, groupId, ct);
        GroupActivityService.Owner(group, actor);
        await GroupAuthorization.MemberAsync(db, group, actor, ct);
        var scope = "character:" + actor;
        var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        ApiFault.Require(characterId != group.OwnerCharacterId, 409, "group_owner_protected", "Group owner permissions are automatic.");
        var member = await GroupAuthorization.MemberAsync(db, group, characterId, ct);
        CommandStore.Match(h, GroupAuthorization.ETag(member)); GroupAuthorization.Validate(body.Capabilities);
        ApiFault.Require(body.Capabilities.All(group.EnabledFeatures.Contains), 403, "group_feature_disabled", "The community owner has not enabled that group feature.");
        member.Capabilities = body.Capabilities.Order().ToArray(); member.PermissionVersion = Guid.CreateVersion7();
        var character = await db.Characters.SingleAsync(x => x.Id == characterId, ct);
        var dto = new GroupMember(character.Id, character.DisplayName, character.HomeWorldName, false, member.Capabilities, GroupAuthorization.ETag(member));
        h.Response.Headers.ETag = dto.Etag;
        var op = commands.New(h, scope, body, actor, communityId, "group.staff");
        op.SubjectCharacterId = characterId; op.Summary = "Group staff permissions updated.";
        var result = await commands.SaveAsync(h, op, new CommandResult<GroupMember>(op.Id, dto), ct);
        await tx.CommitAsync(ct); return result;
    }
}
