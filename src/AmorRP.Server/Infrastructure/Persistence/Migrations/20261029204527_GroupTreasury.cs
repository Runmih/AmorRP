using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupTreasury : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TreasuryOwned",
                table: "groups",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "TreasuryVersion",
                table: "groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "treasury_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    GroupBefore = table.Column<long>(type: "bigint", nullable: false),
                    GroupAfter = table.Column<long>(type: "bigint", nullable: false),
                    CharacterBefore = table.Column<long>(type: "bigint", nullable: false),
                    CharacterAfter = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_treasury_entries", x => x.Id);
                    table.CheckConstraint("ck_treasury_entry_amount", "\"Amount\" > 0 AND \"GroupBefore\" >= 0 AND \"GroupAfter\" >= 0 AND \"CharacterBefore\" >= 0 AND \"CharacterAfter\" >= 0");
                    table.CheckConstraint("ck_treasury_entry_conservation", "(\"Kind\" = 'deposit' AND \"GroupAfter\"::numeric - \"GroupBefore\"::numeric = \"Amount\" AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\") OR (\"Kind\" IN ('withdrawal', 'disband_return') AND \"GroupBefore\"::numeric - \"GroupAfter\"::numeric = \"Amount\" AND \"CharacterAfter\"::numeric - \"CharacterBefore\"::numeric = \"Amount\")");
                    table.ForeignKey(
                        name: "FK_treasury_entries_characters_ActorCharacterId",
                        column: x => x.ActorCharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_treasury_entries_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_treasury_entries_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_treasury_entries_operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_treasury",
                table: "groups",
                sql: "\"TreasuryOwned\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_treasury_entries_ActorCharacterId",
                table: "treasury_entries",
                column: "ActorCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_treasury_entries_CommunityId_CharacterId",
                table: "treasury_entries",
                columns: new[] { "CommunityId", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_treasury_entries_CommunityId_GroupId_Id",
                table: "treasury_entries",
                columns: new[] { "CommunityId", "GroupId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_treasury_entries_OperationId_GroupId",
                table: "treasury_entries",
                columns: new[] { "OperationId", "GroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM treasury_entries) OR EXISTS (SELECT 1 FROM groups WHERE "TreasuryOwned" <> 0) THEN
                        RAISE EXCEPTION 'Cannot remove treasury accounting with balances or receipts. Restore a reviewed backup instead.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "treasury_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_treasury",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "TreasuryOwned",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "TreasuryVersion",
                table: "groups");
        }
    }
}
