using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledEditorialPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledPublishAtUtc",
                table: "EditorialArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialArticles_Status_ScheduledPublishAtUtc",
                table: "EditorialArticles",
                columns: new[] { "Status", "ScheduledPublishAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_EditorialArticles_ScheduledHasPublishTime",
                table: "EditorialArticles",
                sql: "(\"Status\" = 5 AND \"ScheduledPublishAtUtc\" IS NOT NULL) OR (\"Status\" <> 5 AND \"ScheduledPublishAtUtc\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EditorialArticles_Status_ScheduledPublishAtUtc",
                table: "EditorialArticles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EditorialArticles_ScheduledHasPublishTime",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "ScheduledPublishAtUtc",
                table: "EditorialArticles");
        }
    }
}
