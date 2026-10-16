using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LodestoneId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OwnershipKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HomeWorldId = table.Column<string>(type: "text", nullable: false),
                    HomeWorldName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "login_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedVerifier = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    HomeWorldId = table.Column<string>(type: "text", nullable: false),
                    HomeWorldName = table.Column<string>(type: "text", nullable: false),
                    ExistingCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProtectedProviderRefresh = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StateUsed = table.Column<bool>(type: "boolean", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_attempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Delta = table.Column<long>(type: "bigint", nullable: true),
                    BeforeBalance = table.Column<long>(type: "bigint", nullable: true),
                    AfterBalance = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProtectedResponse = table.Column<string>(type: "text", nullable: false),
                    ETag = table.Column<string>(type: "text", nullable: true),
                    ResponseStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OwnerCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CurrencySymbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrencyVersion = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    CurrentPotionPoints = table.Column<int>(type: "integer", nullable: false),
                    CurrentLetters = table.Column<int>(type: "integer", nullable: false),
                    NextPotionPoints = table.Column<int>(type: "integer", nullable: false),
                    NextLetters = table.Column<int>(type: "integer", nullable: false),
                    PolicyPeriod = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.Id);
                    table.CheckConstraint("ck_group_policy", "\"CurrentPotionPoints\" >= 0 AND \"CurrentLetters\" >= 0 AND \"NextPotionPoints\" >= 0 AND \"NextLetters\" >= 0");
                    table.ForeignKey(
                        name: "FK_groups_characters_OwnerCharacterId",
                        column: x => x.OwnerCharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessHash = table.Column<string>(type: "text", nullable: false),
                    AccessExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefreshHash = table.Column<string>(type: "text", nullable: false),
                    RefreshExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProtectedProviderRefresh = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sessions_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaxUses = table.Column<int>(type: "integer", nullable: false),
                    Used = table.Column<int>(type: "integer", nullable: false),
                    Revoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.Id);
                    table.CheckConstraint("ck_invitation_uses", "\"Used\" >= 0 AND \"Used\" <= \"MaxUses\"");
                    table.ForeignKey(
                        name: "FK_invitations_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Capabilities = table.Column<string[]>(type: "text[]", nullable: false),
                    TradingRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Owned = table.Column<long>(type: "bigint", nullable: false),
                    Reserved = table.Column<long>(type: "bigint", nullable: false),
                    BalanceVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => new { x.GroupId, x.CharacterId });
                    table.CheckConstraint("ck_membership_balance", "\"Owned\" >= 0 AND \"Reserved\" >= 0 AND \"Reserved\" <= \"Owned\"");
                    table.CheckConstraint("ck_membership_status", "\"Status\" IN ('active', 'dormant', 'blocked')");
                    table.ForeignKey(
                        name: "FK_memberships_characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_memberships_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ownership_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ownership_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_characters_FromCharacterId",
                        column: x => x.FromCharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_characters_ToCharacterId",
                        column: x => x.ToCharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "used_refresh_tokens",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextRefreshHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_used_refresh_tokens", x => x.TokenHash);
                    table.ForeignKey(
                        name: "FK_used_refresh_tokens_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_characters_LodestoneId",
                table: "characters",
                column: "LodestoneId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_CurrencyId",
                table: "groups",
                column: "CurrencyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_OwnerCharacterId",
                table: "groups",
                column: "OwnerCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_CodeHash",
                table: "invitations",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_GroupId",
                table: "invitations",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_ExpiresAt",
                table: "login_attempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_StateHash",
                table: "login_attempts",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_CharacterId_Status",
                table: "memberships",
                columns: new[] { "CharacterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_operations_GroupId_OccurredAt_Id",
                table: "operations",
                columns: new[] { "GroupId", "OccurredAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_operations_OccurredAt",
                table: "operations",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_operations_Scope_Key",
                table: "operations",
                columns: new[] { "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_FromCharacterId",
                table: "ownership_transfers",
                column: "FromCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_GroupId_Status",
                table: "ownership_transfers",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_ToCharacterId",
                table: "ownership_transfers",
                column: "ToCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_AccessHash",
                table: "sessions",
                column: "AccessHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_CharacterId_CreatedAt",
                table: "sessions",
                columns: new[] { "CharacterId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_RefreshHash",
                table: "sessions",
                column: "RefreshHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_used_refresh_tokens_ExpiresAt",
                table: "used_refresh_tokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_used_refresh_tokens_SessionId",
                table: "used_refresh_tokens",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "login_attempts");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "operations");

            migrationBuilder.DropTable(
                name: "ownership_transfers");

            migrationBuilder.DropTable(
                name: "used_refresh_tokens");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "characters");
        }
    }
}
