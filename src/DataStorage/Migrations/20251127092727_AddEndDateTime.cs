using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddEndDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EventDateTime",
                table: "Items",
                newName: "EventStartDateTime"
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "EventEndDateTime",
                table: "Items",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified)
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EventEndDateTime", table: "Items");

            migrationBuilder.RenameColumn(
                name: "EventStartDateTime",
                table: "Items",
                newName: "EventDateTime"
            );
        }
    }
}
