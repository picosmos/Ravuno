using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ravuno.DataStorage.Migrations
{
    /// <inheritdoc />
    public partial class MigrateConfigFilesToDatabase : Migration
    {
        private const string ConfigFolderPath = "/app/config/updates";
        private const string BackupFolderPath = "/app/config/updates_backup";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            if (!Directory.Exists(ConfigFolderPath))
            {
                Console.WriteLine(
                    $"[Migration] Config folder '{ConfigFolderPath}' does not exist. Skipping file migration."
                );
                return;
            }

            var files = Directory.GetFiles(ConfigFolderPath, "*.*");
            if (files.Length == 0)
            {
                Console.WriteLine(
                    $"[Migration] No configuration files found in '{ConfigFolderPath}'. Skipping file migration."
                );
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    var lines = File.ReadAllLines(file);

                    if (lines.Length < 3)
                    {
                        Console.WriteLine(
                            $"[Migration] WARNING: Configuration file '{file}' does not have enough lines (minimum 3 required). Skipping."
                        );
                        continue;
                    }

                    var queryTitle = lines[0].Trim().TrimStart('-').Trim();
                    var emailLine = lines[1].Trim();
                    var sqlQuery = string.Join(Environment.NewLine, lines.Skip(2)).Trim();

                    if (
                        string.IsNullOrWhiteSpace(queryTitle) || string.IsNullOrWhiteSpace(sqlQuery)
                    )
                    {
                        Console.WriteLine(
                            $"[Migration] WARNING: Configuration file '{file}' has empty required fields. Skipping."
                        );
                        continue;
                    }

                    // Escape single quotes for SQL
                    var escapedTitle = queryTitle.Replace("'", "''");
                    var escapedQuery = sqlQuery.Replace("'", "''");

                    // Insert the SqlScript
                    migrationBuilder.Sql(
                        $@"INSERT INTO ""SqlScripts"" (""Title"", ""Query"") VALUES ('{escapedTitle}', '{escapedQuery}');"
                    );

                    // Parse email addresses
                    List<string> emailAddresses;
                    if (emailLine == "--" || string.IsNullOrWhiteSpace(emailLine))
                    {
                        emailAddresses = [];
                    }
                    else
                    {
                        emailAddresses = emailLine
                            .TrimStart('-')
                            .Split(
                                (char[])null!,
                                StringSplitOptions.RemoveEmptyEntries
                                    | StringSplitOptions.TrimEntries
                            )
                            .ToList();
                    }

                    foreach (var email in emailAddresses)
                    {
                        var escapedEmail = email.Replace("'", "''");

                        // Insert EmailReceiver if not exists
                        migrationBuilder.Sql(
                            $@"INSERT INTO ""EmailReceivers"" (""EmailAddress"")
                               SELECT '{escapedEmail}'
                               WHERE NOT EXISTS (SELECT 1 FROM ""EmailReceivers"" WHERE ""EmailAddress"" = '{escapedEmail}');"
                        );

                        // Create the n-to-m relationship
                        migrationBuilder.Sql(
                            $@"INSERT INTO ""EmailReceiverSqlScript"" (""EmailReceiversId"", ""SqlScriptsId"")
                               SELECT er.""Id"", ss.""Id""
                               FROM ""EmailReceivers"" er, ""SqlScripts"" ss
                               WHERE er.""EmailAddress"" = '{escapedEmail}' AND ss.""Title"" = '{escapedTitle}';"
                        );
                    }

                    Console.WriteLine(
                        $"[Migration] Migrated configuration file '{file}' to database."
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Migration] ERROR: Failed to migrate file '{file}': {ex.Message}"
                    );
                }
            }

            // Rename folder to backup
            try
            {
                if (Directory.Exists(BackupFolderPath))
                {
                    Directory.Delete(BackupFolderPath, true);
                }
                Directory.Move(ConfigFolderPath, BackupFolderPath);
                Console.WriteLine(
                    $"[Migration] WARNING: The config/updates folder has been renamed to config/updates_backup. "
                        + "The SQL configuration files have been transferred to the database. "
                        + "These backup files are no longer needed and can be deleted."
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[Migration] WARNING: Failed to rename config folder to backup: {ex.Message}. "
                        + "Please manually rename or remove the config/updates folder."
                );
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // Remove all migrated data
            migrationBuilder.Sql(@"DELETE FROM ""EmailReceiverSqlScript"";");
            migrationBuilder.Sql(@"DELETE FROM ""SqlScripts"";");
            migrationBuilder.Sql(@"DELETE FROM ""EmailReceivers"";");

            // Restore folder if backup exists
            try
            {
                if (Directory.Exists(BackupFolderPath) && !Directory.Exists(ConfigFolderPath))
                {
                    Directory.Move(BackupFolderPath, ConfigFolderPath);
                    Console.WriteLine($"[Migration] Restored config/updates folder from backup.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[Migration] WARNING: Failed to restore config folder from backup: {ex.Message}"
                );
            }
        }
    }
}
