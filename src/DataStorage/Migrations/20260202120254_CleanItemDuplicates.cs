using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class CleanItemDuplicates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // We had a problem in previous fetch service implementations that caused duplicate
            // items within the db, which caused thereafter to send erroneous updates.
            migrationBuilder.Sql(@"
                DELETE FROM Items
                WHERE Id NOT IN (
                    SELECT MIN(Id)
                    FROM Items
                    GROUP BY Source, SourceId
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No down migration for data cleanup
        }
    }
}