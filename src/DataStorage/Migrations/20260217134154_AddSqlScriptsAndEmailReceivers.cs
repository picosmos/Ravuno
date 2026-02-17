using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddSqlScriptsAndEmailReceivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailReceivers",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EmailAddress = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailReceivers", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "SqlScripts",
                columns: table => new
                {
                    Id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SqlScripts", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "EmailReceiverSqlScript",
                columns: table => new
                {
                    EmailReceiversId = table.Column<long>(type: "INTEGER", nullable: false),
                    SqlScriptsId = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_EmailReceiverSqlScript",
                        x => new { x.EmailReceiversId, x.SqlScriptsId }
                    );
                    table.ForeignKey(
                        name: "FK_EmailReceiverSqlScript_EmailReceivers_EmailReceiversId",
                        column: x => x.EmailReceiversId,
                        principalTable: "EmailReceivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_EmailReceiverSqlScript_SqlScripts_SqlScriptsId",
                        column: x => x.SqlScriptsId,
                        principalTable: "SqlScripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EmailReceivers_EmailAddress",
                table: "EmailReceivers",
                column: "EmailAddress",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_EmailReceiverSqlScript_SqlScriptsId",
                table: "EmailReceiverSqlScript",
                column: "SqlScriptsId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmailReceiverSqlScript");

            migrationBuilder.DropTable(name: "EmailReceivers");

            migrationBuilder.DropTable(name: "SqlScripts");
        }
    }
}
