using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupLogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_bounds",
                table: "media_assets");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "media_assets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "currency");

            migrationBuilder.AddColumn<Guid>(
                name: "LogoAssetId",
                table: "groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogoVersion",
                table: "groups",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_bounds",
                table: "media_assets",
                sql: "(\"Purpose\" = 'currency' AND \"Width\" BETWEEN 1 AND 128 AND \"Height\" BETWEEN 1 AND 128 AND octet_length(\"Png\") <= 131072) OR (\"Purpose\" = 'group_logo' AND \"Width\" BETWEEN 1 AND 512 AND \"Height\" BETWEEN 1 AND 512 AND octet_length(\"Png\") <= 1048576)");

            migrationBuilder.CreateIndex(
                name: "IX_groups_Id_LogoAssetId",
                table: "groups",
                columns: new[] { "Id", "LogoAssetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_groups_media_assets_Id_LogoAssetId",
                table: "groups",
                columns: new[] { "Id", "LogoAssetId" },
                principalTable: "media_assets",
                principalColumns: new[] { "GroupId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_groups_media_assets_Id_LogoAssetId",
                table: "groups");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_bounds",
                table: "media_assets");

            migrationBuilder.DropIndex(
                name: "IX_groups_Id_LogoAssetId",
                table: "groups");

            // Downgrading removes this new feature's images while preserving
            // currency assets and restoring the former strict image bounds.
            migrationBuilder.Sql("DELETE FROM media_assets WHERE \"Purpose\" = 'group_logo'");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "LogoAssetId",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "LogoVersion",
                table: "groups");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_bounds",
                table: "media_assets",
                sql: "\"Width\" BETWEEN 1 AND 128 AND \"Height\" BETWEEN 1 AND 128 AND octet_length(\"Png\") <= 131072");
        }
    }
}
