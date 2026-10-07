using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceFetchLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceFetchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ItemsFound = table.Column<int>(type: "integer", nullable: false),
                    ItemsIngested = table.Column<int>(type: "integer", nullable: false),
                    ItemsSkippedAsDuplicate = table.Column<int>(type: "integer", nullable: false),
                    ErrorSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceFetchLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceFetchLogs_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceFetchLogs_SourceId_StartedAtUtc",
                table: "SourceFetchLogs",
                columns: new[] { "SourceId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceFetchLogs");
        }
    }
}
