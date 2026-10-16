using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TradingBeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Retain M3 identities, written content, balances and weekly usage.
            // Existing category names and allowed types are owner policy. M3 did
            // not mark seed provenance, so neither a name nor a letter allowance
            // proves that a row is an untouched default. Preserve every category;
            // owners opt in to new types through the normal category edit API.
            // New groups receive the Books default from CatalogService.Seed.
            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.AddColumn<Guid>(
                name: "TradeId",
                table: "operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeId",
                table: "letters",
                type: "text",
                nullable: false,
                defaultValue: "letter");

            migrationBuilder.CreateTable(
                name: "trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    InitiatorCurrency = table.Column<long>(type: "bigint", nullable: false),
                    RecipientCurrency = table.Column<long>(type: "bigint", nullable: false),
                    InitiatorConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    RecipientConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndReason = table.Column<string>(type: "text", nullable: true),
                    CompletionOperationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trades", x => new { x.GroupId, x.Id });
                    table.CheckConstraint("ck_trade_amounts", "\"InitiatorCurrency\" >= 0 AND \"RecipientCurrency\" >= 0 AND \"Revision\" > 0");
                    table.CheckConstraint("ck_trade_participants", "\"InitiatorId\" <> \"RecipientId\"");
                    table.CheckConstraint("ck_trade_state", "\"State\" IN ('invited','open','completed','cancelled','expired')");
                    table.ForeignKey(
                        name: "FK_trades_memberships_GroupId_InitiatorId",
                        columns: x => new { x.GroupId, x.InitiatorId },
                        principalTable: "memberships",
                        principalColumns: new[] { "GroupId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trades_memberships_GroupId_RecipientId",
                        columns: x => new { x.GroupId, x.RecipientId },
                        principalTable: "memberships",
                        principalColumns: new[] { "GroupId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trade_lines",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    HoldingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    TypeId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UseMessage = table.Column<string>(type: "text", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_lines", x => new { x.GroupId, x.TradeId, x.CharacterId, x.HoldingId });
                    table.CheckConstraint("ck_trade_line_quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_trade_lines_holdings_GroupId_HoldingId",
                        columns: x => new { x.GroupId, x.HoldingId },
                        principalTable: "holdings",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trade_lines_trades_GroupId_TradeId",
                        columns: x => new { x.GroupId, x.TradeId },
                        principalTable: "trades",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trade_slots",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trade_slots", x => new { x.GroupId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_trade_slots_memberships_GroupId_CharacterId",
                        columns: x => new { x.GroupId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "GroupId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trade_slots_trades_GroupId_TradeId",
                        columns: x => new { x.GroupId, x.TradeId },
                        principalTable: "trades",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" = 'potion' AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");

            migrationBuilder.CreateIndex(
                name: "IX_trade_lines_GroupId_HoldingId",
                table: "trade_lines",
                columns: new[] { "GroupId", "HoldingId" });

            migrationBuilder.CreateIndex(
                name: "IX_trade_slots_GroupId_TradeId",
                table: "trade_slots",
                columns: new[] { "GroupId", "TradeId" });

            migrationBuilder.CreateIndex(
                name: "IX_trades_GroupId_InitiatorId",
                table: "trades",
                columns: new[] { "GroupId", "InitiatorId" });

            migrationBuilder.CreateIndex(
                name: "IX_trades_GroupId_RecipientId",
                table: "trades",
                columns: new[] { "GroupId", "RecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_trades_State_ExpiresAt",
                table: "trades",
                columns: new[] { "State", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM trades) OR EXISTS (SELECT 1 FROM holdings WHERE \"TypeId\" IN ('book','note')) THEN RAISE EXCEPTION 'M4 data exists; use a reviewed backup restore rather than dropping trading/writing history.'; END IF; END $$;");
            migrationBuilder.DropTable(
                name: "trade_lines");

            migrationBuilder.DropTable(
                name: "trade_slots");

            migrationBuilder.DropTable(
                name: "trades");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.DropColumn(
                name: "TradeId",
                table: "operations");

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "letters");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" = 'potion' AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" = 'letter' AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");
        }
    }
}
