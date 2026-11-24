using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpansionThrough016 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "shop_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CommissionPercent",
                table: "group_memberships",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "discord_awards",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_awards", x => x.MessageId);
                    table.CheckConstraint("ck_discord_award_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_discord_awards_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discord_guilds",
                columns: table => new
                {
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    LeaveRequested = table.Column<bool>(type: "boolean", nullable: false),
                    Channels = table.Column<string[]>(type: "text[]", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: true),
                    Paused = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_guilds", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_discord_guilds_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discord_link_codes",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "text", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_link_codes", x => x.Hash);
                    table.ForeignKey(
                        name: "FK_discord_link_codes_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discord_players",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_players", x => new { x.CommunityId, x.UserId });
                    table.ForeignKey(
                        name: "FK_discord_players_memberships_CommunityId_CharacterId",
                        columns: x => new { x.CommunityId, x.CharacterId },
                        principalTable: "memberships",
                        principalColumns: new[] { "CommunityId", "CharacterId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discord_reward_policies",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CoinsPerPost = table.Column<int>(type: "integer", nullable: false),
                    MinimumCharacters = table.Column<int>(type: "integer", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                    WeeklyLimit = table.Column<int>(type: "integer", nullable: false),
                    SeasonWeeklyBudget = table.Column<int>(type: "integer", nullable: false),
                    BalanceCeiling = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_reward_policies", x => x.CommunityId);
                    table.ForeignKey(
                        name: "FK_discord_reward_policies_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_shifts",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_shifts", x => new { x.CommunityId, x.GroupId, x.Id });
                    table.CheckConstraint("ck_group_shift_range", "\"End\" > \"Start\"");
                    table.ForeignKey(
                        name: "FK_group_shifts_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_shifts_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_stock",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_stock", x => new { x.CommunityId, x.GroupId, x.Id });
                    table.CheckConstraint("ck_group_stock_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_group_stock_definition_revisions_CommunityId_DefinitionId_R~",
                        columns: x => new { x.CommunityId, x.DefinitionId, x.Revision },
                        principalTable: "definition_revisions",
                        principalColumns: new[] { "CommunityId", "DefinitionId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_stock_groups_CommunityId_GroupId",
                        columns: x => new { x.CommunityId, x.GroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permit_transfers",
                columns: table => new
                {
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PermitId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permit_transfers", x => new { x.CommunityId, x.Id });
                    table.CheckConstraint("ck_permit_transfer", " \"Price\" >= 0 AND \"SellerGroupId\" <> \"BuyerGroupId\" AND \"State\" IN ('pending', 'approved', 'cancelled', 'completed') ");
                    table.ForeignKey(
                        name: "FK_permit_transfers_groups_CommunityId_BuyerGroupId",
                        columns: x => new { x.CommunityId, x.BuyerGroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permit_transfers_groups_CommunityId_SellerGroupId",
                        columns: x => new { x.CommunityId, x.SellerGroupId },
                        principalTable: "groups",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permit_transfers_ownership_permits_CommunityId_PermitId",
                        columns: x => new { x.CommunityId, x.PermitId },
                        principalTable: "ownership_permits",
                        principalColumns: new[] { "CommunityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_staff_commission",
                table: "group_memberships",
                sql: "\"CommissionPercent\" BETWEEN 0 AND 100");

            migrationBuilder.CreateIndex(
                name: "IX_discord_awards_CommunityId_CharacterId_OccurredAt",
                table: "discord_awards",
                columns: new[] { "CommunityId", "CharacterId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_discord_awards_CommunityId_OccurredAt",
                table: "discord_awards",
                columns: new[] { "CommunityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_discord_awards_CommunityId_UserId_OccurredAt",
                table: "discord_awards",
                columns: new[] { "CommunityId", "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_discord_guilds_CommunityId_GroupId",
                table: "discord_guilds",
                columns: new[] { "CommunityId", "GroupId" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_discord_link_codes_CommunityId_CharacterId_GroupId",
                table: "discord_link_codes",
                columns: new[] { "CommunityId", "CharacterId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_discord_players_CommunityId_CharacterId",
                table: "discord_players",
                columns: new[] { "CommunityId", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_shifts_CharacterId",
                table: "group_shifts",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_group_shifts_CommunityId_GroupId_CharacterId_End",
                table: "group_shifts",
                columns: new[] { "CommunityId", "GroupId", "CharacterId", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_group_stock_CommunityId_DefinitionId_Revision",
                table: "group_stock",
                columns: new[] { "CommunityId", "DefinitionId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_group_stock_CommunityId_GroupId_DefinitionId_Revision",
                table: "group_stock",
                columns: new[] { "CommunityId", "GroupId", "DefinitionId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permit_transfers_CommunityId_BuyerGroupId",
                table: "permit_transfers",
                columns: new[] { "CommunityId", "BuyerGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_permit_transfers_CommunityId_PermitId_State_ExpiresAt",
                table: "permit_transfers",
                columns: new[] { "CommunityId", "PermitId", "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_permit_transfers_CommunityId_SellerGroupId",
                table: "permit_transfers",
                columns: new[] { "CommunityId", "SellerGroupId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discord_awards");

            migrationBuilder.DropTable(
                name: "discord_guilds");

            migrationBuilder.DropTable(
                name: "discord_link_codes");

            migrationBuilder.DropTable(
                name: "discord_players");

            migrationBuilder.DropTable(
                name: "discord_reward_policies");

            migrationBuilder.DropTable(
                name: "group_shifts");

            migrationBuilder.DropTable(
                name: "group_stock");

            migrationBuilder.DropTable(
                name: "permit_transfers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_staff_commission",
                table: "group_memberships");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "shop_products");

            migrationBuilder.DropColumn(
                name: "CommissionPercent",
                table: "group_memberships");
        }
    }
}
