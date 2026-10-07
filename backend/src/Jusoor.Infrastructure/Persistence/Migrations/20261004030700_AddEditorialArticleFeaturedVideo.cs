using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialArticleFeaturedVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FeaturedVideoMediaAssetId",
                table: "EditorialArticles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialArticles_FeaturedVideoMediaAssetId",
                table: "EditorialArticles",
                column: "FeaturedVideoMediaAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_EditorialArticles_EditorialMediaAssets_FeaturedVideoMediaAs~",
                table: "EditorialArticles",
                column: "FeaturedVideoMediaAssetId",
                principalTable: "EditorialMediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EditorialArticles_EditorialMediaAssets_FeaturedVideoMediaAs~",
                table: "EditorialArticles");

            migrationBuilder.DropIndex(
                name: "IX_EditorialArticles_FeaturedVideoMediaAssetId",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "FeaturedVideoMediaAssetId",
                table: "EditorialArticles");
        }
    }
}
