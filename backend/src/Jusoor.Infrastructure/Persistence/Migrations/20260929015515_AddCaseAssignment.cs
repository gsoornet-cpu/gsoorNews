using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssignedAtUtc",
                table: "Cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToCrisisEditorUserId",
                table: "Cases",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_AssignedToCrisisEditorUserId",
                table: "Cases",
                column: "AssignedToCrisisEditorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cases_AssignedToCrisisEditorUserId",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AssignedAtUtc",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AssignedToCrisisEditorUserId",
                table: "Cases");
        }
    }
}
