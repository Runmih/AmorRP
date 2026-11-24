using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CombatAndCharacterProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.AddColumn<bool>(
                name: "CombatRestricted",
                table: "memberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GearJson",
                table: "definition_revisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatPoints",
                table: "definition_revisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BlacksmithingPoints",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "CombatPolicyVersion",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "DownedEffectJson",
                table: "communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResetPotionLimit",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ResetPotionVersion",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "StatPointAllowance",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.CreateTable(
                name: "combat_sheets",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Allocated = table.Column<int[]>(type: "integer[]", nullable: false),
                    BonusPoints = table.Column<int>(type: "integer", nullable: false),
                    HitPoints = table.Column<int>(type: "integer", nullable: false),
                    WeaponId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArmorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResetPotions = table.Column<int>(type: "integer", nullable: false),
                    ResetPurchases = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_combat_sheets", x => new { x.CommunityId, x.CharacterId });
                    table.CheckConstraint("ck_combat_allocated", "cardinality(\"Allocated\") = 6 AND 0 <= ALL(\"Allocated\") AND 10 >= ALL(\"Allocated\")");
                    table.CheckConstraint("ck_combat_hp", "\"HitPoints\" BETWEEN 0 AND 20");
                    table.CheckConstraint("ck_combat_points_and_potions", "\"BonusPoints\" BETWEEN 0 AND 60 AND \"ResetPurchases\" >= \"ResetPotions\" AND \"ResetPotions\" >= 0");
                    table.ForeignKey(
                        name: "FK_combat_sheets_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" IN ('potion','poison','explosive','drug','medicine','meal','drink','dessert','alchemy-supplies','cooking-supplies','weapon','armor') AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");
            migrationBuilder.Sql("""
                INSERT INTO categories ("CommunityId", "Id", "Name", "AllowedTypeIds", "Retired", "Version")
                SELECT "Id", md5("Id"::text || ':blacksmithing')::uuid, 'Blacksmithing', ARRAY['weapon','armor'], false, 1
                FROM communities WHERE NOT "Deleted"
                ON CONFLICT ("CommunityId", "Id") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM categories
                WHERE "Id" = md5("CommunityId"::text || ':blacksmithing')::uuid
                  AND NOT EXISTS (SELECT 1 FROM definition_revisions r
                                  WHERE r."CommunityId" = categories."CommunityId" AND r."CategoryId" = categories."Id");
                """);
            migrationBuilder.DropTable(
                name: "combat_sheets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.DropColumn(
                name: "CombatRestricted",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "GearJson",
                table: "definition_revisions");

            migrationBuilder.DropColumn(
                name: "StatPoints",
                table: "definition_revisions");

            migrationBuilder.DropColumn(
                name: "BlacksmithingPoints",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "CombatPolicyVersion",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "DownedEffectJson",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "ResetPotionLimit",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "ResetPotionVersion",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "StatPointAllowance",
                table: "communities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" IN ('potion','poison','explosive','drug','medicine','meal','drink','dessert','alchemy-supplies','cooking-supplies') AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");
        }
    }
}
