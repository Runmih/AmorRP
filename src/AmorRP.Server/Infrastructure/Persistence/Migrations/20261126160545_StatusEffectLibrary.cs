using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StatusEffectLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MoodleEffectJson",
                table: "definition_revisions",
                newName: "StatusEffectJson");

            migrationBuilder.AddColumn<Guid>(
                name: "StatusEffectId",
                table: "moodle_grants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "status_effect_revisions",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    EffectJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_status_effect_revisions", x => new { x.CommunityId, x.Id, x.Revision });
                    table.ForeignKey(
                        name: "FK_status_effect_revisions_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Keep the original grant IDs and expiry times, and pin every existing recipe to its old visual.
            migrationBuilder.Sql("""
                WITH legacy AS (
                    SELECT "CommunityId", "StatusEffectJson" AS payload
                    FROM definition_revisions WHERE "StatusEffectJson" IS NOT NULL
                    UNION
                    SELECT "CommunityId", "EffectJson" FROM moodle_grants
                )
                INSERT INTO status_effect_revisions ("CommunityId", "Id", "Revision", "EffectJson")
                SELECT "CommunityId", md5(payload)::uuid, 1,
                    jsonb_build_object('Id', md5(payload)::uuid, 'Revision', 1,
                        'Name', payload::jsonb->>'Name', 'DurationSeconds', (payload::jsonb->>'DurationSeconds')::integer,
                        'Modifier', 'display', 'Amount', 0, 'Ability', NULL, 'Moodle', payload::jsonb)::text
                FROM legacy;

                UPDATE definition_revisions AS recipe SET "StatusEffectJson" = effect."EffectJson"
                FROM status_effect_revisions AS effect
                WHERE recipe."CommunityId" = effect."CommunityId" AND md5(recipe."StatusEffectJson")::uuid = effect."Id";

                UPDATE moodle_grants AS grant_row SET "StatusEffectId" = effect."Id", "EffectJson" = effect."EffectJson"
                FROM status_effect_revisions AS effect
                WHERE grant_row."CommunityId" = effect."CommunityId" AND md5(grant_row."EffectJson")::uuid = effect."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_moodle_grants_CommunityId_CharacterId_StatusEffectId",
                table: "moodle_grants",
                columns: new[] { "CommunityId", "CharacterId", "StatusEffectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE definition_revisions SET "StatusEffectJson" = "StatusEffectJson"::jsonb->>'Moodle'
                WHERE "StatusEffectJson" IS NOT NULL;
                DELETE FROM moodle_grants WHERE "EffectJson"::jsonb->>'Moodle' IS NULL;
                UPDATE moodle_grants SET "EffectJson" = "EffectJson"::jsonb->>'Moodle';
                """);

            migrationBuilder.DropTable(
                name: "status_effect_revisions");

            migrationBuilder.DropIndex(
                name: "IX_moodle_grants_CommunityId_CharacterId_StatusEffectId",
                table: "moodle_grants");

            migrationBuilder.DropColumn(
                name: "StatusEffectId",
                table: "moodle_grants");

            migrationBuilder.RenameColumn(
                name: "StatusEffectJson",
                table: "definition_revisions",
                newName: "MoodleEffectJson");
        }
    }
}
