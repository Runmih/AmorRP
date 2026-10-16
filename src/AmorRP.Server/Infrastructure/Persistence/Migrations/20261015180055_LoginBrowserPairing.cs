using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoginBrowserPairing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PairingCodeHash",
                table: "login_attempts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PairingFailures",
                table: "login_attempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Old browser approvals never had a pairing code. Keep saved sessions,
            // but retire every in-flight pre-pairing handoff during the upgrade.
            migrationBuilder.Sql("UPDATE login_attempts SET \"Status\" = 'cancelled', \"ProtectedVerifier\" = '', \"ProtectedProviderRefresh\" = NULL, \"PairingCodeHash\" = NULL WHERE \"SessionId\" IS NULL AND \"Status\" IN ('pending', 'verified');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE login_attempts SET \"Status\" = 'cancelled', \"ProtectedVerifier\" = '', \"ProtectedProviderRefresh\" = NULL WHERE \"SessionId\" IS NULL AND \"Status\" IN ('pending', 'verified');");
            migrationBuilder.DropColumn(
                name: "PairingCodeHash",
                table: "login_attempts");

            migrationBuilder.DropColumn(
                name: "PairingFailures",
                table: "login_attempts");
        }
    }
}
