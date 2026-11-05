using AmorRP.Contracts.Common;
using AmorRP.Contracts.Groups;
using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
namespace AmorRP.Server.Features.Groups;

public sealed partial class GroupService
{
    public async Task<IResult> TrustAsync(HttpContext h, Guid communityId, Guid groupId, GroupTrustEdit body, CancellationToken ct)
    {
        var actor = (await sessions.GetAsync(h, ct)).CharacterId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CommandStore.LockAsync(db, ["character:" + actor, "community:" + communityId], ct);
        await communities.RequireAsync(communityId, actor, ct); var group = await RequireAsync(communityId, groupId, ct);
        var scope = "character:" + actor; var replay = await commands.ReplayAsync(h, scope, body, ct); if (replay != null) return commands.Replay(replay);
        var trust = await db.GroupTrusts.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.GroupId == groupId && x.CharacterId == actor, ct);
        CommandStore.Match(h, TrustEtag(groupId, actor, trust?.Version ?? 0));
        ApiFault.Require(!body.Trusted || group.EnabledFeatures.Contains(GroupCapabilities.Treasury), 403, "group_feature_disabled", "The community owner must enable this group's treasury before automatic billing can be trusted.");
        if (trust == null) { trust = new GroupTrustRow { CommunityId = communityId, GroupId = groupId, CharacterId = actor }; db.GroupTrusts.Add(trust); }
        trust.Trusted = body.Trusted; trust.Version++; await LoadViewerAsync(actor, communityId, ct); viewerTrusts[groupId] = trust;
        var op = commands.New(h, scope, body, actor, communityId, "group.trust"); op.Summary = body.Trusted ? "Automatic billing trusted for this group." : "Automatic billing trust removed; existing debts remain payable.";
        var result = await commands.SaveAsync(h, op, new CommandResult<Group>(op.Id, View(group)), ct);
        await tx.CommitAsync(ct); return result;
    }
    public static string TrustEtag(Guid groupId, Guid actor, int version) => $"\"group-trust:{groupId:N}:{actor:N}:{version}\"";
}
