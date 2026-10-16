using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    public partial class AddEditorialAdPlacementsAndAuthorImage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorImageUrl",
                table: "EditorialArticles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EditorialAdPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Surface = table.Column<int>(type: "integer", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialAdPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EditorialAdPlacements_EditorialMediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "EditorialMediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAdPlacements_MediaAssetId",
                table: "EditorialAdPlacements",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialAdPlacements_SlotKey_Surface",
                table: "EditorialAdPlacements",
                columns: new[] { "SlotKey", "Surface" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EditorialAdPlacements");
            migrationBuilder.DropColumn(name: "AuthorImageUrl", table: "EditorialArticles");
        }
    }
}
