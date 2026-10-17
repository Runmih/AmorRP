using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityGroupActivitiesAndCrafting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.AddColumn<string>(
                name: "JoinMode",
                table: "groups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerCharacterId",
                table: "groups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentCookingPoints",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NextCookingPoints",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "group_announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_announcements", x => new { x.CommunityId, x.Id });
                    table.ForeignKey(
                        name: "FK_group_announcements_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_events", x => new { x.CommunityId, x.Id });
                    table.CheckConstraint("ck_group_event_range", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_group_events_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_favorites",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_favorites", x => new { x.CommunityId, x.GroupId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_group_favorites_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_favorites_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_memberships",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memberships", x => new { x.CommunityId, x.GroupId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_group_memberships_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_memberships_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_notices",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Reward = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_notices", x => new { x.CommunityId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_group_notices_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" IN ('potion','poison','explosive','drug','medicine','meal','drink','dessert') AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");

            migrationBuilder.CreateIndex(
                name: "IX_groups_OwnerCharacterId_Deleted",
                table: "groups",
                columns: new[] { "OwnerCharacterId", "Deleted" });

            migrationBuilder.CreateIndex(
                name: "IX_group_announcements_CommunityId_GroupId_PublishedAt",
                table: "group_announcements",
                columns: new[] { "CommunityId", "GroupId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_group_events_CommunityId_GroupId_EndsAt",
                table: "group_events",
                columns: new[] { "CommunityId", "GroupId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_group_favorites_CommunityId_CharacterId",
                table: "group_favorites",
                columns: new[] { "CommunityId", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_CommunityId_CharacterId",
                table: "group_memberships",
                columns: new[] { "CommunityId", "CharacterId" });

            // Existing profiles predate creator ownership. Preserve them and make
            // the community owner their initial owner/member. Never expand custom categories.
            migrationBuilder.Sql("""
                UPDATE groups g SET "OwnerCharacterId" = c."OwnerCharacterId", "JoinMode" = 'open'
                FROM communities c WHERE c."Id" = g."CommunityId";
                INSERT INTO group_memberships ("CommunityId", "GroupId", "CharacterId")
                SELECT g."CommunityId", g."Id", g."OwnerCharacterId" FROM groups g;
                INSERT INTO categories ("Id", "CommunityId", "Name", "AllowedTypeIds", "Retired", "Version")
                SELECT gen_random_uuid(), c."Id", 'Alchemy', ARRAY['potion','poison','explosive','drug','medicine']::text[], false, 1
                FROM communities c WHERE NOT EXISTS (SELECT 1 FROM categories x WHERE x."CommunityId" = c."Id" AND x."Name" = 'Alchemy' AND x."AllowedTypeIds" = ARRAY['potion','poison','explosive','drug','medicine']::text[]);
                INSERT INTO categories ("Id", "CommunityId", "Name", "AllowedTypeIds", "Retired", "Version")
                SELECT gen_random_uuid(), c."Id", 'Cooking', ARRAY['meal','drink','dessert']::text[], false, 1
                FROM communities c WHERE NOT EXISTS (SELECT 1 FROM categories x WHERE x."CommunityId" = c."Id" AND x."Name" = 'Cooking' AND x."AllowedTypeIds" = ARRAY['meal','drink','dessert']::text[]);
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_groups_characters_OwnerCharacterId",
                table: "groups",
                column: "OwnerCharacterId",
                principalTable: "characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM groups g JOIN communities c ON c."Id"=g."CommunityId" WHERE g."OwnerCharacterId"<>c."OwnerCharacterId" OR g."JoinMode"<>'open' OR g."PasswordHash" IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM group_notices) OR EXISTS (SELECT 1 FROM group_events)
                    OR EXISTS (SELECT 1 FROM group_announcements) OR EXISTS (SELECT 1 FROM group_favorites)
                    OR EXISTS (SELECT 1 FROM group_memberships m JOIN groups g ON g."CommunityId"=m."CommunityId" AND g."Id"=m."GroupId" WHERE m."CharacterId"<>g."OwnerCharacterId")
                    OR EXISTS (SELECT 1 FROM item_definitions WHERE "TypeId" NOT IN ('potion'))
                    OR EXISTS (SELECT 1 FROM quota_usage WHERE "Kind"='cooking_points' AND "Used">0)
                  THEN RAISE EXCEPTION 'Group activity or new crafting data exists; restore a matching backup instead of discarding it.';
                  END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_groups_characters_OwnerCharacterId",
                table: "groups");

            migrationBuilder.DropTable(
                name: "group_announcements");

            migrationBuilder.DropTable(
                name: "group_events");

            migrationBuilder.DropTable(
                name: "group_favorites");

            migrationBuilder.DropTable(
                name: "group_memberships");

            migrationBuilder.DropTable(
                name: "group_notices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.DropIndex(
                name: "IX_groups_OwnerCharacterId_Deleted",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "JoinMode",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "OwnerCharacterId",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "CurrentCookingPoints",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "NextCookingPoints",
                table: "communities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" = 'potion' AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");
        }
    }
}
