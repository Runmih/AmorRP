using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrustedBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CollectionPending",
                table: "memberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "LastCollectionAttempt",
                table: "memberships",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "AllowDebt",
                table: "communities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BillingPolicyVersion",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "bills",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupName = table.Column<string>(type: "text", nullable: false),
                    IssuerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerName = table.Column<string>(type: "text", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    Service = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Collected = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bills", x => new { x.CommunityId, x.Id });
                    table.CheckConstraint("ck_bill_amount", "\"Amount\" > 0 AND \"Collected\" >= 0 AND \"Collected\" <= \"Amount\"");
                    table.ForeignKey(
                        name: "FK_bills_characters_IssuerCharacterId",
                        column: x => x.IssuerCharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bills_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bills_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_trusts",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trusted = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_trusts", x => new { x.CommunityId, x.GroupId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_group_trusts_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_trusts_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bill_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    CharacterBefore = table.Column<long>(type: "bigint", nullable: false),
                    CharacterAfter = table.Column<long>(type: "bigint", nullable: false),
                    TreasuryBefore = table.Column<long>(type: "bigint", nullable: false),
                    TreasuryAfter = table.Column<long>(type: "bigint", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_payments", x => x.Id);
                    table.CheckConstraint("ck_bill_payment", "\"Amount\" > 0 AND \"CharacterAfter\" >= 0 AND \"TreasuryBefore\" >= 0 AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\" AND \"TreasuryAfter\"::numeric - \"TreasuryBefore\"::numeric = \"Amount\"");
                    table.ForeignKey(
                        name: "FK_bill_payments_bills_CommunityId_BillId",
                        columns: x => new { x.CommunityId, x.BillId },
                        principalTable: "bills",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_CollectionPending_LastCollectionAttempt",
                table: "memberships",
                columns: new[] { "CollectionPending", "LastCollectionAttempt" });

            migrationBuilder.CreateIndex(
                name: "IX_bill_payments_CommunityId_BillId_Id",
                table: "bill_payments",
                columns: new[] { "CommunityId", "BillId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_bills_CommunityId_CharacterId_CreatedAt_Id",
                table: "bills",
                columns: new[] { "CommunityId", "CharacterId", "CreatedAt", "Id" },
                filter: "\"Collected\" < \"Amount\"");

            migrationBuilder.CreateIndex(
                name: "IX_bills_CommunityId_GroupId_IssuerCharacterId_CharacterId_Cre~",
                table: "bills",
                columns: new[] { "CommunityId", "GroupId", "IssuerCharacterId", "CharacterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bills_IssuerCharacterId",
                table: "bills",
                column: "IssuerCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_group_trusts_CommunityId_CharacterId",
                table: "group_trusts",
                columns: new[] { "CommunityId", "CharacterId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM bills) OR EXISTS (SELECT 1 FROM bill_payments)
                       OR EXISTS (SELECT 1 FROM memberships WHERE "CollectionPending") THEN
                        RAISE EXCEPTION 'Cannot remove billing with financial records or held repayments. Restore a reviewed backup instead.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "bill_payments");

            migrationBuilder.DropTable(
                name: "group_trusts");

            migrationBuilder.DropTable(
                name: "bills");

            migrationBuilder.DropIndex(
                name: "IX_memberships_CollectionPending_LastCollectionAttempt",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "CollectionPending",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "LastCollectionAttempt",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "AllowDebt",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "BillingPolicyVersion",
                table: "communities");
        }
    }
}
