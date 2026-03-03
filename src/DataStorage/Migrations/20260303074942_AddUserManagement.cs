using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Ravuno.DataStorage.Constants;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class AddUserManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Salt = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(name: "IX_Users_RoleId", table: "Users", column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true
            );

            // Seed roles
            migrationBuilder.InsertData(
                table: "Roles",
                columns: ["Id", "Name"],
                values: new object[,]
                {
                    { 1, RoleNames.Admin },
                    { 2, RoleNames.User },
                }
            );

            // Seed default admin user with username "admin" and password "password"
            // Using a fixed salt for reproducibility
            var saltBytes = new byte[]
            {
                0x01,
                0x02,
                0x03,
                0x04,
                0x05,
                0x06,
                0x07,
                0x08,
                0x09,
                0x0a,
                0x0b,
                0x0c,
                0x0d,
                0x0e,
                0x0f,
                0x10,
                0x11,
                0x12,
                0x13,
                0x14,
                0x15,
                0x16,
                0x17,
                0x18,
                0x19,
                0x1a,
                0x1b,
                0x1c,
                0x1d,
                0x1e,
                0x1f,
                0x20,
            };
            var salt = Convert.ToBase64String(saltBytes);

            var passwordBytes = System.Text.Encoding.UTF8.GetBytes("password");
            var combined = new byte[saltBytes.Length + passwordBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
            Buffer.BlockCopy(passwordBytes, 0, combined, saltBytes.Length, passwordBytes.Length);

            var hashBytes = System.Security.Cryptography.SHA256.HashData(combined);
            var passwordHash = Convert.ToBase64String(hashBytes);

            migrationBuilder.InsertData(
                table: "Users",
                columns: ["Id", "Username", "PasswordHash", "Salt", "RoleId", "CreatedAt"],
                values: [1, "admin", passwordHash, salt, 1, DateTime.UtcNow]
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 1);

            migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValue: 1);

            migrationBuilder.DeleteData(table: "Roles", keyColumn: "Id", keyValue: 2);

            migrationBuilder.DropTable(name: "Groups");

            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.DropTable(name: "Roles");
        }
    }
}
