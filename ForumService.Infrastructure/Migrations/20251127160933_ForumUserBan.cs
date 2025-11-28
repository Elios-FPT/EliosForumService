using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ForumUserBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForumUserBans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BannedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    BannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BanUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UnbannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnbannedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UnbanReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumUserBans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ForumUserBans_UserId",
                table: "ForumUserBans",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ForumUserBans");
        }
    }
}
