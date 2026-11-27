using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefineCombatPointBuy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_combat_allocated",
                table: "combat_sheets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_combat_hp",
                table: "combat_sheets");

            migrationBuilder.AlterColumn<int>(
                name: "StatPointAllowance",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 27,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 6);

            // Keep purchased scores, convert the default budget, and preserve existing wounds.
            migrationBuilder.Sql("""
                UPDATE communities
                SET "StatPointAllowance" = 27, "CombatPolicyVersion" = "CombatPolicyVersion" + 1
                WHERE "StatPointAllowance" = 6;

                UPDATE combat_sheets AS sheet SET
                    "Allocated" = ARRAY(
                        SELECT CASE WHEN points = 0 THEN 0 ELSE points + 2 END
                        FROM unnest(sheet."Allocated") WITH ORDINALITY AS scores(points, position)
                        ORDER BY position),
                    "HitPoints" = CASE WHEN sheet."HitPoints" = 0 THEN 0 ELSE greatest(1,
                        sheet."HitPoints" + floor((CASE WHEN sheet."Allocated"[3] = 0 THEN -2 ELSE sheet."Allocated"[3] END) / 2.0)::integer) END,
                    "Version" = sheet."Version" + 1;

                -- A die with 2*Power-1 sides preserves the old weapon's average base damage.
                UPDATE definition_revisions SET "GearJson" = (
                    "GearJson"::jsonb || jsonb_build_object('Form', 'melee', 'Value', 0, 'DiceCount', 1,
                        'DiceSides', 2 * ("GearJson"::jsonb->>'Value')::integer - 1))::text
                WHERE "GearJson"::jsonb->>'Form' IN ('sword', 'staff');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_combat_allocated",
                table: "combat_sheets",
                sql: "cardinality(\"Allocated\") = 6 AND 0 <= ALL(\"Allocated\") AND 12 >= ALL(\"Allocated\")");

            migrationBuilder.AddCheckConstraint(
                name: "ck_combat_hp",
                table: "combat_sheets",
                sql: "\"HitPoints\" BETWEEN 0 AND 20 + floor((\"Allocated\"[3] - 2) / 2.0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_combat_allocated",
                table: "combat_sheets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_combat_hp",
                table: "combat_sheets");

            migrationBuilder.AlterColumn<int>(
                name: "StatPointAllowance",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 6,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 27);

            migrationBuilder.Sql("""
                UPDATE communities
                SET "StatPointAllowance" = 6, "CombatPolicyVersion" = "CombatPolicyVersion" + 1
                WHERE "StatPointAllowance" = 27;

                UPDATE combat_sheets AS sheet SET
                    "Allocated" = ARRAY(
                        SELECT greatest(0, points - 2)
                        FROM unnest(sheet."Allocated") WITH ORDINALITY AS scores(points, position)
                        ORDER BY position),
                    "HitPoints" = CASE WHEN sheet."HitPoints" = 0 THEN 0 ELSE greatest(1, least(20,
                        sheet."HitPoints" - floor((sheet."Allocated"[3] - 2) / 2.0)::integer)) END,
                    "Version" = sheet."Version" + 1;

                UPDATE definition_revisions SET "GearJson" = (
                    ("GearJson"::jsonb - 'DiceCount' - 'DiceSides') || jsonb_build_object('Form', 'sword',
                        'Ability', CASE WHEN "GearJson"::jsonb->>'Ability' = 'DEX' THEN 'DEX' ELSE 'STR' END,
                        'Value', greatest(1, least(100, (("GearJson"::jsonb->>'DiceCount')::integer
                            * (("GearJson"::jsonb->>'DiceSides')::integer + 1)) / 2))))::text
                WHERE "GearJson"::jsonb->>'Form' = 'melee';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_combat_allocated",
                table: "combat_sheets",
                sql: "cardinality(\"Allocated\") = 6 AND 0 <= ALL(\"Allocated\") AND 10 >= ALL(\"Allocated\")");

            migrationBuilder.AddCheckConstraint(
                name: "ck_combat_hp",
                table: "combat_sheets",
                sql: "\"HitPoints\" BETWEEN 0 AND 20");
        }
    }
}
