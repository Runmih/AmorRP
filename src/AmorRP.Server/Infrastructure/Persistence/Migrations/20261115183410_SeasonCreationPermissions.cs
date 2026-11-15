using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeasonCreationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE communities SET \"SeasonState\" = 'draft' WHERE \"SeasonState\" = 'legacy'");
            migrationBuilder.Sql("UPDATE memberships SET \"Capabilities\" = array_replace(\"Capabilities\", 'community.moderate', 'season.moderate')");
            migrationBuilder.AlterColumn<string>(
                name: "SeasonState",
                table: "communities",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "draft",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "legacy");

            migrationBuilder.CreateTable(
                name: "season_creation_permissions",
                columns: table => new
                {
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanCreate = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_season_creation_permissions", x => x.CharacterId);
                    table.ForeignKey(
                        name: "FK_season_creation_permissions_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_season_state",
                table: "communities",
                sql: "\"SeasonState\" IN ('draft', 'active', 'archived')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "season_creation_permissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_season_state",
                table: "communities");

            migrationBuilder.AlterColumn<string>(
                name: "SeasonState",
                table: "communities",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "legacy",
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16,
                oldDefaultValue: "draft");
        }
    }
}
