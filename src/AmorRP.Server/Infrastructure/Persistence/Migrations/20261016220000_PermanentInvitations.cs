using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AmorRP.Server.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AmorDbContext))]
[Migration("20261016220000_PermanentInvitations")]
public sealed class PermanentInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        UPDATE invitations SET "ExpiresAt" = TIMESTAMPTZ '9999-01-01 00:00:00+00', "MaxUses" = 2147483647
        WHERE NOT "Revoked" AND "ExpiresAt" > CURRENT_TIMESTAMP AND "Used" < "MaxUses";
        """);
    // A permanent invitation has no recoverable former expiry or use limit.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
