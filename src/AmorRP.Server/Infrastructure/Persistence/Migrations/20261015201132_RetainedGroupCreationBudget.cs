using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetainedGroupCreationBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByCharacterId",
                table: "groups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Recover the immutable creator from the original create receipt.
            // Legacy rows without that evidence count against their current owner.
            // This is resource accounting only; current ownership never changes.
            migrationBuilder.Sql("""
                UPDATE groups g SET "CreatedByCharacterId" = COALESCE(
                    (SELECT o."ActorCharacterId" FROM operations o
                     JOIN characters c ON c."Id" = o."ActorCharacterId"
                     WHERE o."GroupId" = g."Id" AND o."Kind" = 'group.create'
                     ORDER BY o."OccurredAt", o."Id" LIMIT 1), g."OwnerCharacterId");
                ALTER TABLE groups ALTER COLUMN "CreatedByCharacterId" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_groups_CreatedByCharacterId",
                table: "groups",
                column: "CreatedByCharacterId");

            migrationBuilder.AddForeignKey(
                name: "FK_groups_characters_CreatedByCharacterId",
                table: "groups",
                column: "CreatedByCharacterId",
                principalTable: "characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_groups_characters_CreatedByCharacterId",
                table: "groups");

            migrationBuilder.DropIndex(
                name: "IX_groups_CreatedByCharacterId",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "CreatedByCharacterId",
                table: "groups");
        }
    }
}
