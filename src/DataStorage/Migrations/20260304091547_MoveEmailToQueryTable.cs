using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class MoveEmailToQueryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add Email column to Queries table
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Queries",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: ""
            );

            // Step 2: Migrate data from EmailReceiverQuery join table to Queries.Email
            migrationBuilder.Sql(
                @"UPDATE Queries 
                  SET Email = (
                      SELECT EmailReceivers.EmailAddress 
                      FROM EmailReceiverQuery 
                      JOIN EmailReceivers ON EmailReceiverQuery.EmailReceiversId = EmailReceivers.Id 
                      WHERE EmailReceiverQuery.QueriesId = Queries.Id 
                      LIMIT 1
                  )"
            );

            // Step 3: Drop the join table and EmailReceivers table
            migrationBuilder.DropTable(name: "EmailReceiverQuery");

            migrationBuilder.DropTable(name: "EmailReceivers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Email", table: "Queries");

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
                name: "EmailReceiverQuery",
                columns: table => new
                {
                    EmailReceiversId = table.Column<long>(type: "INTEGER", nullable: false),
                    QueriesId = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_EmailReceiverQuery",
                        x => new { x.EmailReceiversId, x.QueriesId }
                    );
                    table.ForeignKey(
                        name: "FK_EmailReceiverQuery_EmailReceivers_EmailReceiversId",
                        column: x => x.EmailReceiversId,
                        principalTable: "EmailReceivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_EmailReceiverQuery_Queries_QueriesId",
                        column: x => x.QueriesId,
                        principalTable: "Queries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_EmailReceiverQuery_QueriesId",
                table: "EmailReceiverQuery",
                column: "QueriesId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_EmailReceivers_EmailAddress",
                table: "EmailReceivers",
                column: "EmailAddress",
                unique: true
            );
        }
    }
}
