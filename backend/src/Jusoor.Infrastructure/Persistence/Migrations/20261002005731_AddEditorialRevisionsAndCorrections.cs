using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialRevisionsAndCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EditorialArticleRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EditedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EditorRoles = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EditedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialArticleRevisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EditorialCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsMajor = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IssuerRoles = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialCorrections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialArticleRevisions_ArticleId_RevisionNumber",
                table: "EditorialArticleRevisions",
                columns: new[] { "ArticleId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialArticleRevisions_EditedByUserId_EditedAtUtc",
                table: "EditorialArticleRevisions",
                columns: new[] { "EditedByUserId", "EditedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCorrections_ArticleId_IssuedAtUtc",
                table: "EditorialCorrections",
                columns: new[] { "ArticleId", "IssuedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditorialArticleRevisions");

            migrationBuilder.DropTable(
                name: "EditorialCorrections");
        }
    }
}
