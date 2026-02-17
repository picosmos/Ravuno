using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class IdAsKey : Migration
    {
        // I did a mistake while configuring the initial primary key for the Items table.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // For SQLite we can't alter primary key or make a column AUTOINCREMENT in-place.
            // Create a new table with Id as INTEGER PRIMARY KEY AUTOINCREMENT, copy rows
            // so each row gets a unique sequential Id, then replace the old table.
            migrationBuilder.Sql(
                """
                CREATE TABLE Items_temp (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Source TEXT,
                    RetrievedAt TEXT,
                    Url TEXT,
                    Description TEXT,
                    EnrollmentDeadline TEXT,
                    EventEndDateTime TEXT,
                    EventStartDateTime TEXT,
                    Location TEXT,
                    Organizer TEXT,
                    Price TEXT,
                    RawData TEXT,
                    SourceId TEXT NOT NULL,
                    Tags TEXT,
                    Title TEXT
                );

                INSERT INTO Items_temp (Source, RetrievedAt, Url, Description, EnrollmentDeadline, EventEndDateTime, EventStartDateTime, Location, Organizer, Price, RawData, SourceId, Tags, Title)
                SELECT Source, RetrievedAt, Url, Description, EnrollmentDeadline, EventEndDateTime, EventStartDateTime, Location, Organizer, Price, RawData, SourceId, Tags, Title
                FROM Items;

                DROP TABLE Items;
                ALTER TABLE Items_temp RENAME TO Items;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to the previous schema: composite primary key on (Source, RetrievedAt, Url) and
            // Id present but defaulting to 0 (as it was after AddItemId migration).
            migrationBuilder.Sql(
                """
                CREATE TABLE Items_old (
                    Source TEXT,
                    RetrievedAt TEXT,
                    Url TEXT,
                    Description TEXT,
                    EnrollmentDeadline TEXT,
                    EventEndDateTime TEXT,
                    EventStartDateTime TEXT,
                    Id INTEGER NOT NULL DEFAULT 0,
                    Location TEXT,
                    Organizer TEXT,
                    Price TEXT,
                    RawData TEXT,
                    SourceId TEXT NOT NULL,
                    Tags TEXT,
                    Title TEXT,
                    PRIMARY KEY (Source, RetrievedAt, Url)
                );

                INSERT INTO Items_old (Source, RetrievedAt, Url, Description, EnrollmentDeadline, EventEndDateTime, EventStartDateTime, Id, Location, Organizer, Price, RawData, SourceId, Tags, Title)
                SELECT Source, RetrievedAt, Url, Description, EnrollmentDeadline, EventEndDateTime, EventStartDateTime, Id, Location, Organizer, Price, RawData, SourceId, Tags, Title
                FROM Items;

                DROP TABLE Items;
                ALTER TABLE Items_old RENAME TO Items;
                """
            );
        }
    }
}
