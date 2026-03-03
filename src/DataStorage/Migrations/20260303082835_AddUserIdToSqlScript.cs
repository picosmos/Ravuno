using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToSqlScript : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "SqlScripts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.CreateIndex(
                name: "IX_SqlScripts_UserId",
                table: "SqlScripts",
                column: "UserId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_SqlScripts_Users_UserId",
                table: "SqlScripts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SqlScripts_Users_UserId",
                table: "SqlScripts"
            );

            migrationBuilder.DropIndex(name: "IX_SqlScripts_UserId", table: "SqlScripts");

            migrationBuilder.DropColumn(name: "UserId", table: "SqlScripts");
        }
    }
}
