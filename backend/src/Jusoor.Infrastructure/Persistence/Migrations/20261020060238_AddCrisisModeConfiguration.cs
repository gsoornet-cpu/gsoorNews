using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jusoor.Infrastructure.Persistence.Migrations;

public partial class AddCrisisModeConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CrisisModes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                SourceName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                AffectedCountryIdsJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastModifiedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_CrisisModes", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_CrisisModes_Key", table: "CrisisModes", column: "Key", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "CrisisModes");
}
