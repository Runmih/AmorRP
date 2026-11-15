using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Seasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeasonState",
                table: "communities",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.CreateIndex(
                name: "IX_communities_SeasonState",
                table: "communities",
                column: "SeasonState",
                unique: true,
                filter: "\"SeasonState\" = 'active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_communities_SeasonState",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "SeasonState",
                table: "communities");
        }
    }
}
