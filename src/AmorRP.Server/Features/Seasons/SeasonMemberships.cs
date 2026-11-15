using AmorRP.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AmorRP.Server.Features.Seasons;

// Season membership is shared by every verified character. This is not account moderation.
public static class SeasonMemberships
{
    public static Task<int> EnrollAsync(AmorDbContext db, Guid? actor, Guid? season, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO memberships ("CommunityId", "CharacterId", "Status", "ReadAnnouncementIds", "Description", "Capabilities", "TradingRestricted", "Version", "LastCollectionAttempt", "CollectionPending", "Owned", "Reserved", "BalanceVersion")
            SELECT s."Id", c."Id", 'active', ARRAY[]::uuid[], '', ARRAY[]::text[], false, 1, 0, false, 0, 0, 1
            FROM communities s CROSS JOIN characters c
            WHERE NOT s."Deleted" AND ({actor}::uuid IS NULL OR c."Id" = {actor})
                AND ({season}::uuid IS NULL OR s."Id" = {season})
            ON CONFLICT ("CommunityId", "CharacterId") DO NOTHING
            """, ct);
}
