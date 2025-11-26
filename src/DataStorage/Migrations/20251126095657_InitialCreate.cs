using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataStorage.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Items",
            columns: table => new
            {
                Source = table.Column<string>(type: "TEXT", nullable: false),
                RetrievedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                RawData = table.Column<string>(type: "TEXT", nullable: true),
                EventDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Location = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                Price = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                EnrollmentDeadline = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Items", x => new { x.Source, x.RetrievedAt, x.Url });
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Items");
    }
}