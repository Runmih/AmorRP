using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeasonShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_products",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionRevision = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    Buyer = table.Column<string>(type: "text", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: true),
                    BuyerLimit = table.Column<int>(type: "integer", nullable: true),
                    Listed = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_products", x => new { x.CommunityId, x.Id });
                    table.CheckConstraint("ck_shop_product", " \"Price\" > 0 AND \"Buyer\" IN ('character', 'group', 'either') AND \"Stock\" >= 0 AND \"BuyerLimit\" > 0 ");
                    table.ForeignKey(
                        name: "FK_shop_products_definition_revisions_CommunityId_DefinitionId~",
                        columns: x => new { x.CommunityId, x.DefinitionId, x.DefinitionRevision },
                        principalTable: "definition_revisions",
                        principalColumns: new[] { "CommunityId", "DefinitionId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_purchases", x => x.Id);
                    table.CheckConstraint("ck_shop_purchase", " \"Quantity\" > 0 ");
                    table.ForeignKey(
                        name: "FK_shop_purchases_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shop_purchases_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shop_purchases_shop_products_CommunityId_ProductId",
                        columns: x => new { x.CommunityId, x.ProductId },
                        principalTable: "shop_products",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shop_products_CommunityId_DefinitionId_DefinitionRevision",
                table: "shop_products",
                columns: new[] { "CommunityId", "DefinitionId", "DefinitionRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_purchases_CommunityId_CharacterId",
                table: "shop_purchases",
                columns: new[] { "CommunityId", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_purchases_CommunityId_GroupId",
                table: "shop_purchases",
                columns: new[] { "CommunityId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_purchases_CommunityId_ProductId_CharacterId",
                table: "shop_purchases",
                columns: new[] { "CommunityId", "ProductId", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_shop_purchases_CommunityId_ProductId_GroupId",
                table: "shop_purchases",
                columns: new[] { "CommunityId", "ProductId", "GroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM shop_products) OR EXISTS (SELECT 1 FROM shop_purchases)
                    THEN RAISE EXCEPTION 'Cannot discard shop records. Restore a matching backup.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "shop_purchases");

            migrationBuilder.DropTable(
                name: "shop_products");
        }
    }
}
