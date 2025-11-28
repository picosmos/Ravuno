using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddItemProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Organizer",
                table: "Items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Items",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Organizer",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Items");
        }
    }
}