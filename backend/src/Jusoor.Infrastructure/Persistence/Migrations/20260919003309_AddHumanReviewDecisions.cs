using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHumanReviewDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HumanReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: false),
                    IsRelevant = table.Column<bool>(type: "boolean", nullable: false),
                    Reasoning = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HumanReviewDecisions_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HumanReviewDecisions_ReviewedByUserId_DecidedAtUtc",
                table: "HumanReviewDecisions",
                columns: new[] { "ReviewedByUserId", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HumanReviewDecisions_StoryId_DecidedAtUtc",
                table: "HumanReviewDecisions",
                columns: new[] { "StoryId", "DecidedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HumanReviewDecisions");
        }
    }
}
