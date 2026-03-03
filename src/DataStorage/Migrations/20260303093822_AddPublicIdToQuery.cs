using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicIdToQuery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Queries",
                type: "TEXT",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.Sql(
                @"UPDATE Queries 
                  SET PublicId = lower(hex(randomblob(16))) 
                  WHERE PublicId IS NULL"
            );

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Queries",
                type: "TEXT",
                maxLength: 100,
                nullable: false
            );

            migrationBuilder.CreateIndex(
                name: "IX_Queries_PublicId",
                table: "Queries",
                column: "PublicId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Queries_PublicId", table: "Queries");

            migrationBuilder.DropColumn(name: "PublicId", table: "Queries");
        }
    }
}
