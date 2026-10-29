using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupStaffPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "EnabledFeatures",
                table: "groups",
                type: "text[]",
                nullable: false,
                defaultValue: new[] { "announcements.manage", "notice.manage", "events.manage" });

            migrationBuilder.AddColumn<string[]>(
                name: "Capabilities",
                table: "group_memberships",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "PermissionVersion",
                table: "group_memberships",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnabledFeatures",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "Capabilities",
                table: "group_memberships");

            migrationBuilder.DropColumn(
                name: "PermissionVersion",
                table: "group_memberships");
        }
    }
}
