using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class IdAsKey2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_Items", table: "Items");

            migrationBuilder
                .AlterColumn<long>(
                    name: "Id",
                    table: "Items",
                    type: "INTEGER",
                    nullable: false,
                    oldClrType: typeof(long),
                    oldType: "INTEGER"
                )
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Items",
                type: "TEXT",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 2000
            );

            migrationBuilder.AddPrimaryKey(name: "PK_Items", table: "Items", column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "PK_Items", table: "Items");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Items",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 2000,
                oldNullable: true
            );

            migrationBuilder
                .AlterColumn<long>(
                    name: "Id",
                    table: "Items",
                    type: "INTEGER",
                    nullable: false,
                    oldClrType: typeof(long),
                    oldType: "INTEGER"
                )
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Items",
                table: "Items",
                columns: ["Source", "RetrievedAt", "Url"]
            );
        }
    }
}
