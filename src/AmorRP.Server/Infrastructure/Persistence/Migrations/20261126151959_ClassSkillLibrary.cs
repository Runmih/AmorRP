using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClassSkillLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "BlockedGameplayGrants",
                table: "memberships",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string>(
                name: "SkillCatalog",
                table: "communities",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "SkillLibraryVersion",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StayedStandingPeriod",
                table: "combat_sheets",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockedGameplayGrants",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "SkillCatalog",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "SkillLibraryVersion",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "StayedStandingPeriod",
                table: "combat_sheets");
        }
    }
}
