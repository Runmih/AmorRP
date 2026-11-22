using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GatheringSupplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_quota_usage",
                table: "quota_usage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_definition_cost",
                table: "definition_revisions");

            migrationBuilder.AddColumn<int>(
                name: "Bonus",
                table: "quota_usage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BonusPoints",
                table: "definition_revisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentGatheringPoints",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GatheringVersion",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NextGatheringPoints",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_quota_usage",
                table: "quota_usage",
                sql: "\"Used\" >= 0 AND \"Bonus\" >= 0 AND \"Used\" <= \"Limit\"::bigint + \"Bonus\" AND \"Limit\"::bigint + \"Bonus\" <= 2147483647");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" IN ('potion','poison','explosive','drug','medicine','meal','drink','dessert','alchemy-supplies','cooking-supplies') AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_definition_cost",
                table: "definition_revisions",
                sql: "\"CreationCost\" > 0 AND \"BonusPoints\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gathering_budget",
                table: "communities",
                sql: " \"CurrentGatheringPoints\" >= 0 AND \"NextGatheringPoints\" >= 0 AND \"GatheringVersion\" >= 0 ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM communities WHERE "GatheringVersion" > 0) OR EXISTS (SELECT 1 FROM quota_usage WHERE "Bonus" > 0)
                    THEN RAISE EXCEPTION 'Cannot discard supply settings or bonus balances. Restore a matching backup.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_quota_usage",
                table: "quota_usage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_definition_cost",
                table: "definition_revisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gathering_budget",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "Bonus",
                table: "quota_usage");

            migrationBuilder.DropColumn(
                name: "BonusPoints",
                table: "definition_revisions");

            migrationBuilder.DropColumn(
                name: "CurrentGatheringPoints",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "GatheringVersion",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "NextGatheringPoints",
                table: "communities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quota_usage",
                table: "quota_usage",
                sql: "\"Used\" >= 0 AND \"Used\" <= \"Limit\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_holding_payload",
                table: "holdings",
                sql: "(\"TypeId\" IN ('potion','poison','explosive','drug','medicine','meal','drink','dessert') AND \"DefinitionId\" IS NOT NULL AND \"DefinitionRevision\" IS NOT NULL AND \"LetterId\" IS NULL) OR (\"TypeId\" IN ('letter','book','note') AND \"LetterId\" IS NOT NULL AND \"DefinitionId\" IS NULL AND \"DefinitionRevision\" IS NULL AND \"Quantity\" <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_definition_cost",
                table: "definition_revisions",
                sql: "\"CreationCost\" > 0");
        }
    }
}
