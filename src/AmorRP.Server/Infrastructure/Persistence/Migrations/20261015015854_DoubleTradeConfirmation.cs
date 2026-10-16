using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DoubleTradeConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve all offers/assets, but require fresh acceptance under the new protocol.
            migrationBuilder.Sql("UPDATE trades SET \"InitiatorConfirmed\" = false, \"RecipientConfirmed\" = false, \"Revision\" = \"Revision\" + 1 WHERE \"State\" IN ('invited','open');");
            migrationBuilder.DropCheckConstraint(
                name: "ck_trade_state",
                table: "trades");

            migrationBuilder.AddColumn<bool>(
                name: "InitiatorFinalConfirmed",
                table: "trades",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RecipientFinalConfirmed",
                table: "trades",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_trade_state",
                table: "trades",
                sql: "\"State\" IN ('invited','open','confirming','completed','cancelled','expired')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM trades WHERE \"State\" = 'confirming' OR \"InitiatorFinalConfirmed\" OR \"RecipientFinalConfirmed\") THEN RAISE EXCEPTION 'Double-confirmation data exists; use a reviewed restore rather than removing consent history.'; END IF; END $$;");
            migrationBuilder.DropCheckConstraint(
                name: "ck_trade_state",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "InitiatorFinalConfirmed",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "RecipientFinalConfirmed",
                table: "trades");

            migrationBuilder.AddCheckConstraint(
                name: "ck_trade_state",
                table: "trades",
                sql: "\"State\" IN ('invited','open','completed','cancelled','expired')");
        }
    }
}
