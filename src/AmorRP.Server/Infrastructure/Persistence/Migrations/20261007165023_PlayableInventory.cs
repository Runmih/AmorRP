using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlayableInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyIconAssetId",
                table: "groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InventoryVersion",
                table: "groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AllowedTypeIds = table.Column<string[]>(type: "text[]", nullable: false),
                    Retired = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => new { x.GroupId, x.Id });
                    table.ForeignKey(
                        name: "FK_categories_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Retired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_definitions", x => new { x.GroupId, x.Id });
                    table.ForeignKey(
                        name: "FK_item_definitions_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Png = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => new { x.GroupId, x.Id });
                    table.CheckConstraint("ck_media_bounds", "\"Width\" BETWEEN 1 AND 128 AND \"Height\" BETWEEN 1 AND 128 AND octet_length(\"Png\") <= 131072");
                    table.ForeignKey(
                        name: "FK_media_assets_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quota_usage",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Limit = table.Column<int>(type: "integer", nullable: false),
                    Used = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quota_usage", x => new { x.GroupId, x.CharacterId, x.Kind, x.PeriodStart });
                    table.CheckConstraint("ck_quota_usage", "\"Used\" >= 0 AND \"Used\" <= \"Limit\"");
                    table.ForeignKey(
                        name: "FK_quota_usage_memberships_GroupId_CharacterId",
                        columns: x => new { x.GroupId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "GroupId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "letters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    FirstTradedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letters", x => new { x.GroupId, x.Id });
                    table.ForeignKey(
                        name: "FK_letters_categories_GroupId_CategoryId",
                        columns: x => new { x.GroupId, x.CategoryId },
                        principalTable: "categories",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_letters_characters_AuthorCharacterId",
                        column: x => x.AuthorCharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "definition_revisions",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TypeDataVersion = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreationCost = table.Column<int>(type: "integer", nullable: false),
                    UseMessage = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_definition_revisions", x => new { x.GroupId, x.DefinitionId, x.Revision });
                    table.CheckConstraint("ck_definition_cost", "\"CreationCost\" > 0");
                    table.ForeignKey(
                        name: "FK_definition_revisions_categories_GroupId_CategoryId",
                        columns: x => new { x.GroupId, x.CategoryId },
                        principalTable: "categories",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_definition_revisions_item_definitions_GroupId_DefinitionId",
                        columns: x => new { x.GroupId, x.DefinitionId },
                        principalTable: "item_definitions",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "holdings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Reserved = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitionRevision = table.Column<int>(type: "integer", nullable: true),
                    LetterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_holdings", x => new { x.GroupId, x.Id });
                    table.CheckConstraint("ck_holding_payload", "(\"TypeId\" = 'potion' AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" = 'letter' AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");
                    table.CheckConstraint("ck_holding_quantity", "\"Quantity\" >= 0 AND \"Reserved\" >= 0 AND \"Reserved\" <= \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_holdings_categories_GroupId_CategoryId",
                        columns: x => new { x.GroupId, x.CategoryId },
                        principalTable: "categories",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_holdings_definition_revisions_GroupId_DefinitionId_Definiti~",
                        columns: x => new { x.GroupId, x.DefinitionId, x.DefinitionRevision },
                        principalTable: "definition_revisions",
                        principalColumns: new[] { "GroupId", "DefinitionId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_holdings_letters_GroupId_LetterId",
                        columns: x => new { x.GroupId, x.LetterId },
                        principalTable: "letters",
                        principalColumns: new[] { "GroupId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_holdings_memberships_GroupId_CharacterId",
                        columns: x => new { x.GroupId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "GroupId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_groups_Id_CurrencyIconAssetId",
                table: "groups",
                columns: new[] { "Id", "CurrencyIconAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_definition_revisions_GroupId_CategoryId",
                table: "definition_revisions",
                columns: new[] { "GroupId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_holdings_GroupId_CategoryId",
                table: "holdings",
                columns: new[] { "GroupId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_holdings_GroupId_CharacterId_DefinitionId_DefinitionRevision",
                table: "holdings",
                columns: new[] { "GroupId", "CharacterId", "DefinitionId", "DefinitionRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_holdings_GroupId_CharacterId_TypeId",
                table: "holdings",
                columns: new[] { "GroupId", "CharacterId", "TypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_holdings_GroupId_DefinitionId_DefinitionRevision",
                table: "holdings",
                columns: new[] { "GroupId", "DefinitionId", "DefinitionRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_holdings_GroupId_LetterId",
                table: "holdings",
                columns: new[] { "GroupId", "LetterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_letters_AuthorCharacterId",
                table: "letters",
                column: "AuthorCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_letters_GroupId_CategoryId",
                table: "letters",
                columns: new[] { "GroupId", "CategoryId" });

            migrationBuilder.AddForeignKey(
                name: "FK_groups_media_assets_Id_CurrencyIconAssetId",
                table: "groups",
                columns: new[] { "Id", "CurrencyIconAssetId" },
                principalTable: "media_assets",
                principalColumns: new[] { "GroupId", "Id" },
                onDelete: ReferentialAction.Restrict);
            // Seed existing M2 groups without replacing their IDs, balances or membership.
            migrationBuilder.Sql("""
                INSERT INTO categories ("GroupId", "Id", "Name", "AllowedTypeIds", "Retired", "Version")
                SELECT "Id", gen_random_uuid(), 'Consumables', ARRAY['potion']::text[], false, 1 FROM groups;
                INSERT INTO categories ("GroupId", "Id", "Name", "AllowedTypeIds", "Retired", "Version")
                SELECT "Id", gen_random_uuid(), 'Correspondence', ARRAY['letter']::text[], false, 1 FROM groups;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_groups_media_assets_Id_CurrencyIconAssetId",
                table: "groups");

            migrationBuilder.DropTable(
                name: "holdings");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "quota_usage");

            migrationBuilder.DropTable(
                name: "definition_revisions");

            migrationBuilder.DropTable(
                name: "letters");

            migrationBuilder.DropTable(
                name: "item_definitions");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropIndex(
                name: "IX_groups_Id_CurrencyIconAssetId",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "CurrencyIconAssetId",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "InventoryVersion",
                table: "groups");
        }
    }
}
