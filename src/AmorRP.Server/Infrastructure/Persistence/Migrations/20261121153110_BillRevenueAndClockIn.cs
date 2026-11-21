using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillRevenueAndClockIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments");

            migrationBuilder.AddColumn<int>(
                name: "BillingSettingsVersion",
                table: "groups",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CommissionPercent",
                table: "groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireClockIn",
                table: "groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClockedInAt",
                table: "group_memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingTaxPercent",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SeasonTreasuryOwned",
                table: "communities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "CommissionPercent",
                table: "bills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaxPercent",
                table: "bills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Commission",
                table: "bill_payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SeasonTax",
                table: "bill_payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_commission",
                table: "groups",
                sql: "\"CommissionPercent\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_season_billing_tax",
                table: "communities",
                sql: "\"BillingTaxPercent\" BETWEEN 0 AND 100 AND \"SeasonTreasuryOwned\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_rates",
                table: "bills",
                sql: "\"TaxPercent\" BETWEEN 0 AND 100 AND \"CommissionPercent\" BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments",
                sql: "\"Amount\" > 0 AND \"CharacterAfter\" >= 0 AND \"TreasuryBefore\" >= 0 AND \"TreasuryAfter\" >= \"TreasuryBefore\" AND \"SeasonTax\" >= 0 AND \"Commission\" >= 0 AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\" AND \"TreasuryAfter\"::numeric - \"TreasuryBefore\"::numeric + \"SeasonTax\" + \"Commission\" = \"Amount\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_group_commission",
                table: "groups");

            migrationBuilder.DropCheckConstraint(
                name: "ck_season_billing_tax",
                table: "communities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_rates",
                table: "bills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments");

            migrationBuilder.DropColumn(
                name: "BillingSettingsVersion",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "RequireClockIn",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "ClockedInAt",
                table: "group_memberships");

            migrationBuilder.DropColumn(
                name: "BillingTaxPercent",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "SeasonTreasuryOwned",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "TaxPercent",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Commission",
                table: "bill_payments");

            migrationBuilder.DropColumn(
                name: "SeasonTax",
                table: "bill_payments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bill_payment",
                table: "bill_payments",
                sql: "\"Amount\" > 0 AND \"CharacterAfter\" >= 0 AND \"TreasuryBefore\" >= 0 AND \"CharacterBefore\"::numeric - \"CharacterAfter\"::numeric = \"Amount\" AND \"TreasuryAfter\"::numeric - \"TreasuryBefore\"::numeric = \"Amount\"");
        }
    }
}
