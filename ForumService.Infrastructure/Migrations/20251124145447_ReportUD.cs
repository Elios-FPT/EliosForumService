using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportUD : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModeratorNote",
                table: "Reports",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeratorNote",
                table: "Reports");
        }
    }
}
