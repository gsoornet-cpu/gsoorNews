using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEditorialMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EditorialMediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Credit = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FocalPointX = table.Column<int>(type: "integer", nullable: true),
                    FocalPointY = table.Column<int>(type: "integer", nullable: true),
                    UploadedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UploadExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialMediaAssets", x => x.Id);
                    table.CheckConstraint("CK_EditorialMediaAssets_FocalPointPair", "(\"FocalPointX\" IS NULL AND \"FocalPointY\" IS NULL) OR (\"FocalPointX\" BETWEEN 0 AND 100 AND \"FocalPointY\" BETWEEN 0 AND 100)");
                    table.CheckConstraint("CK_EditorialMediaAssets_ReadyTimestamp", "\"Status\" <> 2 OR \"ReadyAtUtc\" IS NOT NULL");
                    table.CheckConstraint("CK_EditorialMediaAssets_SizePositive", "\"SizeBytes\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialMediaAssets_ObjectPath",
                table: "EditorialMediaAssets",
                column: "ObjectPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EditorialMediaAssets_Status_CreatedAtUtc",
                table: "EditorialMediaAssets",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialMediaAssets_UploadedByUserId_UploadExpiresAtUtc",
                table: "EditorialMediaAssets",
                columns: new[] { "UploadedByUserId", "UploadExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditorialMediaAssets");
        }
    }
}
