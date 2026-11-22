using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnershipPermits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments");

            migrationBuilder.AddColumn<bool>(
                name: "PermitLocked",
                table: "group_trusts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PermitEnabled",
                table: "communities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PermitPrice",
                table: "communities",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingGroupId",
                table: "bill_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "permit_listings",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permit_listings", x => new { x.CommunityId, x.CharacterId });
                    table.CheckConstraint("ck_permit_listing_status", " \"Status\" IN ('pending', 'listed', 'withdrawn', 'completed') ");
                    table.ForeignKey(
                        name: "FK_permit_listings_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permit_offers",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Private = table.Column<bool>(type: "boolean", nullable: false),
                    ListingVersion = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AcceptedQuote = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permit_offers", x => new { x.CommunityId, x.Id });
                    table.CheckConstraint("ck_permit_offer_status", " \"Status\" IN ('pending', 'withdrawn', 'completed') AND \"CharacterId\" <> \"BuyerId\" ");
                    table.ForeignKey(
                        name: "FK_permit_offers_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permit_offers_memberships_CommunityId_BuyerId",
                        columns: x => new { x.CommunityId, x.BuyerId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permit_offers_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ownership_permits",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ownership_permits", x => new { x.CommunityId, x.Id });
                    table.ForeignKey(
                        name: "FK_ownership_permits_permit_offers_CommunityId_Id",
                        columns: x => new { x.CommunityId, x.Id },
                        principalTable: "permit_offers",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_permit_price",
                table: "communities",
                sql: " \"PermitPrice\" > 0 ");

            migrationBuilder.CreateIndex(
                name: "IX_bill_payments_CommunityId_FundingGroupId",
                table: "bill_payments",
                columns: new[] { "CommunityId", "FundingGroupId" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments",
                sql: "\"Amount\" > 0 AND \"CharacterAfter\" >= 0 AND \"TreasuryBefore\" >= 0 AND \"TreasuryAfter\" >= \"TreasuryBefore\" AND \"SeasonTax\" >= 0 AND \"Commission\" >= 0 AND ((\"FundingGroupId\" IS NULL AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\") OR (\"FundingGroupId\" IS NOT NULL AND \"CharacterBefore\" = \"CharacterAfter\")) AND \"TreasuryAfter\"::numeric - \"TreasuryBefore\"::numeric + \"SeasonTax\" + \"Commission\" = \"Amount\"");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_permits_CommunityId_CharacterId",
                table: "ownership_permits",
                columns: new[] { "CommunityId", "CharacterId" },
                unique: true,
                filter: " \"ReleasedAt\" IS NULL ");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_permits_CommunityId_GroupId_ReleasedAt",
                table: "ownership_permits",
                columns: new[] { "CommunityId", "GroupId", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_permit_offers_CommunityId_BuyerId_Status_ExpiresAt",
                table: "permit_offers",
                columns: new[] { "CommunityId", "BuyerId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_permit_offers_CommunityId_CharacterId_Status_ExpiresAt",
                table: "permit_offers",
                columns: new[] { "CommunityId", "CharacterId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_permit_offers_CommunityId_GroupId",
                table: "permit_offers",
                columns: new[] { "CommunityId", "GroupId" });

            migrationBuilder.AddForeignKey(
                name: "FK_bill_payments_groups_CommunityId_FundingGroupId",
                table: "bill_payments",
                columns: new[] { "CommunityId", "FundingGroupId" },
                principalTable: "groups",
                principalColumns: new[] { "CommunityId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ownership_permits)
                        OR EXISTS (SELECT 1 FROM permit_offers)
                        OR EXISTS (SELECT 1 FROM permit_listings)
                    THEN RAISE EXCEPTION 'Cannot discard permit records. Restore a matching backup.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_bill_payments_groups_CommunityId_FundingGroupId",
                table: "bill_payments");

            migrationBuilder.DropTable(
                name: "ownership_permits");

            migrationBuilder.DropTable(
                name: "permit_listings");

            migrationBuilder.DropTable(
                name: "permit_offers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_permit_price",
                table: "communities");

            migrationBuilder.DropIndex(
                name: "IX_bill_payments_CommunityId_FundingGroupId",
                table: "bill_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments");

            migrationBuilder.DropColumn(
                name: "PermitLocked",
                table: "group_trusts");

            migrationBuilder.DropColumn(
                name: "PermitEnabled",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "PermitPrice",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "FundingGroupId",
                table: "bill_payments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments",
                sql: "\"Amount\" > 0 AND \"CharacterAfter\" >= 0 AND \"TreasuryBefore\" >= 0 AND \"TreasuryAfter\" >= \"TreasuryBefore\" AND \"SeasonTax\" >= 0 AND \"Commission\" >= 0 AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\" AND \"TreasuryAfter\"::numeric - \"TreasuryBefore\"::numeric + \"SeasonTax\" + \"Commission\" = \"Amount\"");
        }
    }
}
