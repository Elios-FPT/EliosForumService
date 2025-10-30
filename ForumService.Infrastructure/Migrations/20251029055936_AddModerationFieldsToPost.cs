using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForumService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationFieldsToPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAt",
                table: "Posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModeratedBy",
                table: "Posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Posts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeratedAt",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ModeratedBy",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Posts");
        }
    }
}
