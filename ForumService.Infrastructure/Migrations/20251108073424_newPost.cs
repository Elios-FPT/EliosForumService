using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class newPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "Posts",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Posts");
        }
    }
}
