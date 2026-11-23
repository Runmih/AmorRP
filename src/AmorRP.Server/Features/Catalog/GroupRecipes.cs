using AmorRP.Server.Infrastructure.Persistence;
using AmorRP.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Catalog;

internal static class GroupRecipes
{
    public static IQueryable<Guid> Memberships(AmorDbContext db, Guid season, Guid actor) =>
        from member in db.GroupMemberships
        join entry in db.Groups on new { member.CommunityId, Id = member.GroupId } equals new { entry.CommunityId, entry.Id }
        where member.CommunityId == season && member.CharacterId == actor && !entry.Deleted
        select entry.Id;

    public static async Task RequireAsync(AmorDbContext db, Guid season, Guid actor, Guid? group, CancellationToken ct)
    {
        if (group is { } id)
            ApiFault.Require(await Memberships(db, season, actor).ContainsAsync(id, ct), 404, "not_found", "Recipe unavailable.");
    }
}
