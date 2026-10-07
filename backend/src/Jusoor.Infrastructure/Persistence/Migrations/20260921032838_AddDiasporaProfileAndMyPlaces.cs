using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiasporaProfileAndMyPlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiasporaProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PrivacyLevel = table.Column<int>(type: "integer", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CityId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiasporaProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiasporaProfiles_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiasporaProfiles_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiasporaProfiles_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MyPlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyPlaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyPlaces_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiasporaProfiles_CityId",
                table: "DiasporaProfiles",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_DiasporaProfiles_CountryId",
                table: "DiasporaProfiles",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_DiasporaProfiles_RegionId",
                table: "DiasporaProfiles",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_DiasporaProfiles_UserId",
                table: "DiasporaProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MyPlaces_CityId",
                table: "MyPlaces",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_MyPlaces_UserId_CityId",
                table: "MyPlaces",
                columns: new[] { "UserId", "CityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiasporaProfiles");

            migrationBuilder.DropTable(
                name: "MyPlaces");
        }
    }
}
