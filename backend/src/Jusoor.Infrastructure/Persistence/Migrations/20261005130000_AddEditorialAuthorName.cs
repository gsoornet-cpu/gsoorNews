using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261005130000_AddEditorialAuthorName")]
    public partial class AddEditorialAuthorName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "EditorialArticles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "EditorialArticleRevisions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AuthorName", table: "EditorialArticles");
            migrationBuilder.DropColumn(name: "AuthorName", table: "EditorialArticleRevisions");
        }
    }
}
