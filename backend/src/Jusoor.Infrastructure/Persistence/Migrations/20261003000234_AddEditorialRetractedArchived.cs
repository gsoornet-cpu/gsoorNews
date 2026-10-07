using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialRetractedArchived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "EditorialArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedByUserId",
                table: "EditorialArticles",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetractedAtUtc",
                table: "EditorialArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetractedByUserId",
                table: "EditorialArticles",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetractionNotice",
                table: "EditorialArticles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_EditorialArticles_RetractedHasNotice",
                table: "EditorialArticles",
                sql: "\"Status\" <> 6 OR \"RetractionNotice\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EditorialArticles_RetractedHasNotice",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "RetractedAtUtc",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "RetractedByUserId",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "RetractionNotice",
                table: "EditorialArticles");
        }
    }
}
