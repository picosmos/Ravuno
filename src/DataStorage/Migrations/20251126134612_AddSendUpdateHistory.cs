using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddSendUpdateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SendUpdateHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QueryTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    EmailReceiverAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NewItemsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedItemsCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SendUpdateHistories", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SendUpdateHistories");
        }
    }
}
