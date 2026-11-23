using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClassesGroupRecipesAndMoodles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "ClassCapabilities",
                table: "memberships",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<Guid[]>(
                name: "ClassIds",
                table: "memberships",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::uuid[]");

            migrationBuilder.AddColumn<int>(
                name: "ClassSelectionVersion",
                table: "memberships",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "item_definitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MoodleEffectJson",
                table: "definition_revisions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClassCatalog",
                table: "communities",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "ClassPolicyVersion",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "ClassResetPrice",
                table: "communities",
                type: "bigint",
                nullable: false,
                defaultValue: 1000L);

            migrationBuilder.AddColumn<int>(
                name: "ClassSlots",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "GroupRecipeLimit",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<string>(
                name: "OwnershipEffectJson",
                table: "communities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnershipEffectVersion",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "moodle_grants",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectJson = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moodle_grants", x => new { x.CommunityId, x.CharacterId, x.Id });
                    table.ForeignKey(
                        name: "FK_moodle_grants_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_definitions_CommunityId_GroupId",
                table: "item_definitions",
                columns: new[] { "CommunityId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_moodle_grants_CharacterId_ExpiresAt",
                table: "moodle_grants",
                columns: new[] { "CharacterId", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_item_definitions_groups_CommunityId_GroupId",
                table: "item_definitions",
                columns: new[] { "CommunityId", "GroupId" },
                principalTable: "groups",
                principalColumns: new[] { "CommunityId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_item_definitions_groups_CommunityId_GroupId",
                table: "item_definitions");

            migrationBuilder.DropTable(
                name: "moodle_grants");

            migrationBuilder.DropIndex(
                name: "IX_item_definitions_CommunityId_GroupId",
                table: "item_definitions");

            migrationBuilder.DropColumn(
                name: "ClassCapabilities",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "ClassIds",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "ClassSelectionVersion",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "item_definitions");

            migrationBuilder.DropColumn(
                name: "MoodleEffectJson",
                table: "definition_revisions");

            migrationBuilder.DropColumn(
                name: "ClassCatalog",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "ClassPolicyVersion",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "ClassResetPrice",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "ClassSlots",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "GroupRecipeLimit",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "OwnershipEffectJson",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "OwnershipEffectVersion",
                table: "communities");
        }
    }
}
