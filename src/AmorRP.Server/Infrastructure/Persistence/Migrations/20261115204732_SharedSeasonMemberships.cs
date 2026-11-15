using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedSeasonMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_membership_status",
                table: "memberships");

            // Old season exclusions are retired. Restore participation without reviving grants.
            migrationBuilder.Sql("UPDATE memberships SET \"Status\" = 'active', \"Capabilities\" = ARRAY[]::text[], \"Version\" = \"Version\" + 1 WHERE \"Status\" <> 'active'");
            migrationBuilder.Sql("""
                INSERT INTO memberships ("CommunityId", "CharacterId", "Status", "ReadAnnouncementIds", "Description", "Capabilities", "TradingRestricted", "Version", "LastCollectionAttempt", "CollectionPending", "Owned", "Reserved", "BalanceVersion")
                SELECT s."Id", c."Id", 'active', ARRAY[]::uuid[], '', ARRAY[]::text[], false, 1, 0, false, 0, 0, 1
                FROM communities s CROSS JOIN characters c WHERE NOT s."Deleted"
                ON CONFLICT ("CommunityId", "CharacterId") DO NOTHING
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_membership_status",
                table: "memberships",
                sql: "\"Status\" = 'active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_membership_status",
                table: "memberships");

            migrationBuilder.AddCheckConstraint(
                name: "ck_membership_status",
                table: "memberships",
                sql: "\"Status\" IN ('active', 'dormant', 'blocked')");
        }
    }
}
