using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialSeoTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryCategoryId",
                table: "EditorialArticles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "SecondaryTags",
                table: "EditorialArticles",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "TwitterDescription",
                table: "EditorialArticles",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterImageUrl",
                table: "EditorialArticles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterTitle",
                table: "EditorialArticles",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EditorialCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialCategories", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "EditorialCategories" ("Id", "NameAr", "Slug", "DisplayOrder", "IsActive", "CreatedAtUtc") VALUES
                ('e3000000-0000-4000-8000-000000000001', 'أخبار مصر', 'egypt-news', 1, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000002', 'أخبار المغتربين', 'diaspora-news', 2, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000003', 'إقامة وتأشيرات وقانون', 'residency-visas-law', 3, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000004', 'اقتصاد وتحويلات', 'economy-remittances', 4, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000005', 'عمل وتعليم', 'work-education', 5, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000006', 'صحة وأسرة', 'health-family', 6, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000007', 'سفر وطيران', 'travel-aviation', 7, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000008', 'ثقافة ومجتمع', 'culture-society', 8, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000009', 'رياضة', 'sports', 9, TRUE, CURRENT_TIMESTAMP),
                ('e3000000-0000-4000-8000-000000000010', 'تكنولوجيا', 'technology', 10, TRUE, CURRENT_TIMESTAMP);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialArticles_PrimaryCategoryId",
                table: "EditorialArticles",
                column: "PrimaryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCategories_IsActive_DisplayOrder",
                table: "EditorialCategories",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialCategories_Slug",
                table: "EditorialCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EditorialArticles_EditorialCategories_PrimaryCategoryId",
                table: "EditorialArticles",
                column: "PrimaryCategoryId",
                principalTable: "EditorialCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EditorialArticles_EditorialCategories_PrimaryCategoryId",
                table: "EditorialArticles");

            migrationBuilder.DropTable(
                name: "EditorialCategories");

            migrationBuilder.DropIndex(
                name: "IX_EditorialArticles_PrimaryCategoryId",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "PrimaryCategoryId",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "SecondaryTags",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "TwitterDescription",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "TwitterImageUrl",
                table: "EditorialArticles");

            migrationBuilder.DropColumn(
                name: "TwitterTitle",
                table: "EditorialArticles");
        }
    }
}
