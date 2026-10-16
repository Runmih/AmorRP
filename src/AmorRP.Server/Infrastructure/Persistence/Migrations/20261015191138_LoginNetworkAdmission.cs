using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoginNetworkAdmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginHash",
                table: "login_attempts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "login_attempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_OriginHash_ExpiresAt",
                table: "login_attempts",
                columns: new[] { "OriginHash", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_login_attempts_OriginHash_StartedAt",
                table: "login_attempts",
                columns: new[] { "OriginHash", "StartedAt" });

            // Earlier handoffs have no trustworthy origin. Retire only unfinished
            // ones so an existing anonymous backlog cannot bypass admission.
            // Existing saved sessions, characters and RP assets are untouched.
            migrationBuilder.Sql("UPDATE login_attempts SET \"Status\" = 'cancelled', \"ProtectedVerifier\" = '', \"ProtectedProviderRefresh\" = NULL, \"PairingCodeHash\" = NULL WHERE \"SessionId\" IS NULL AND \"Status\" IN ('pending', 'verified');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_login_attempts_OriginHash_ExpiresAt",
                table: "login_attempts");

            migrationBuilder.DropIndex(
                name: "IX_login_attempts_OriginHash_StartedAt",
                table: "login_attempts");

            migrationBuilder.DropColumn(
                name: "OriginHash",
                table: "login_attempts");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "login_attempts");
        }
    }
}
