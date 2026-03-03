using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class RenameSqlScriptToQuery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the SqlScripts table to Queries
            migrationBuilder.RenameTable(
                name: "SqlScripts",
                newName: "Queries");

            // Rename the Query column to SqlQuery
            migrationBuilder.RenameColumn(
                name: "Query",
                table: "Queries",
                newName: "SqlQuery");

            // Rename the junction table
            migrationBuilder.RenameTable(
                name: "EmailReceiverSqlScript",
                newName: "EmailReceiverQuery");

            // Rename the foreign key column in junction table
            migrationBuilder.RenameColumn(
                name: "SqlScriptsId",
                table: "EmailReceiverQuery",
                newName: "QueriesId");

            // Rename the index
            migrationBuilder.RenameIndex(
                name: "IX_SqlScripts_UserId",
                table: "Queries",
                newName: "IX_Queries_UserId");

            // Rename the index on junction table
            migrationBuilder.RenameIndex(
                name: "IX_EmailReceiverSqlScript_SqlScriptsId",
                table: "EmailReceiverQuery",
                newName: "IX_EmailReceiverQuery_QueriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the rename operations
            migrationBuilder.RenameIndex(
                name: "IX_EmailReceiverQuery_QueriesId",
                table: "EmailReceiverQuery",
                newName: "IX_EmailReceiverSqlScript_SqlScriptsId");

            migrationBuilder.RenameIndex(
                name: "IX_Queries_UserId",
                table: "Queries",
                newName: "IX_SqlScripts_UserId");

            migrationBuilder.RenameColumn(
                name: "QueriesId",
                table: "EmailReceiverQuery",
                newName: "SqlScriptsId");

            migrationBuilder.RenameTable(
                name: "EmailReceiverQuery",
                newName: "EmailReceiverSqlScript");

            migrationBuilder.RenameColumn(
                name: "SqlQuery",
                table: "Queries",
                newName: "Query");

            migrationBuilder.RenameTable(
                name: "Queries",
                newName: "SqlScripts");
        }
    }
}
