using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AmorRP.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "ReadAnnouncementIds",
                table: "memberships",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<long>(
                name: "AnnouncementSequence",
                table: "groups",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    AuthorCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => new { x.GroupId, x.Id });
                    table.ForeignKey(
                        name: "FK_announcements_characters_AuthorCharacterId",
                        column: x => x.AuthorCharacterId,
                        principalTable: "characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_announcements_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_AuthorCharacterId",
                table: "announcements",
                column: "AuthorCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_GroupId_Sequence",
                table: "announcements",
                columns: new[] { "GroupId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropColumn(
                name: "ReadAnnouncementIds",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "AnnouncementSequence",
                table: "groups");
        }
    }
}
