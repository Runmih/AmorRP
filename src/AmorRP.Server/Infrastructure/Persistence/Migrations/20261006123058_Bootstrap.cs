using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Bootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "infrastructure_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_infrastructure_state", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "infrastructure_state",
                columns: new[] { "id", "schema_version" },
                values: new object[] { "bootstrap", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "infrastructure_state");
        }
    }
}
