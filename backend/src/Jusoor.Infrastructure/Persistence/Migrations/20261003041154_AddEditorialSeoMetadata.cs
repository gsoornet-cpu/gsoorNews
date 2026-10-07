using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialSeoMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalUrl",
                table: "EditorialArticles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NoFollow",
                table: "EditorialArticles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NoIndex",
                table: "EditorialArticles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SeoDescription",
                table: "EditorialArticles",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeoTitle",
                table: "EditorialArticles",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "EditorialArticles",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialDescription",
                table: "EditorialArticles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialImageUrl",
                table: "EditorialArticles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SocialTitle",
                table: "EditorialArticles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"EditorialArticles\" SET \"Slug\" = 'article-' || replace(\"Id\"::text, '-', '') WHERE \"Slug\" IS NULL OR \"Slug\" = '';");
            migrationBuilder.AlterColumn<string>(
                name: "Slug", table: "EditorialArticles", type: "character varying(180)", maxLength: 180,
                nullable: false, oldClrType: typeof(string), oldType: "character varying(180)", oldMaxLength: 180, oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialArticles_Slug",
                table: "EditorialArticles",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EditorialArticles_Slug",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "CanonicalUrl",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "NoFollow",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "NoIndex",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "SeoDescription",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "SeoTitle",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "SocialDescription",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "SocialImageUrl",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "SocialTitle",
                table: "EditorialArticles");
        }
    }
}
