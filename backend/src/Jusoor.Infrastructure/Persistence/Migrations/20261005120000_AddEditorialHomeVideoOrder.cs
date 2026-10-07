using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Editor-curated homepage video hero: a nullable 1-based position on the article.
    /// Hand-written (no BuildTargetModel): the model snapshot is updated in the same change,
    /// so the next generated migration diffs cleanly against it.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261005120000_AddEditorialHomeVideoOrder")]
    public partial class AddEditorialHomeVideoOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeVideoOrder",
                table: "EditorialArticles",
                type: "integer",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeVideoOrder",
                table: "EditorialArticles");
        }
    }
}
