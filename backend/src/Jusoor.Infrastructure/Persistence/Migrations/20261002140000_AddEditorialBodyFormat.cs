using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialBodyFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Slice 21 (decision D2). defaultValue: 0 is the point of this migration: every row that
            // exists today holds PLAIN TEXT, and 0 = ArticleBodyFormat.PlainText, so existing articles
            // and revision snapshots are back-filled as plain text (they are HTML-encoded on read and
            // rewritten as sanitized HTML the next time someone saves them). No data is modified.
            migrationBuilder.AddColumn<int>(
                name: "BodyFormat",
                table: "EditorialArticles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BodyFormat",
                table: "EditorialArticleRevisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyFormat",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "BodyFormat",
                table: "EditorialArticleRevisions");
        }
    }
}
