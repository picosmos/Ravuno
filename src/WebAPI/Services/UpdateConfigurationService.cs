using WebAPI.Models;
using WebAPI.Services.Contracts;

namespace WebAPI.Services;

public class UpdateConfigurationService : IUpdateConfigurationService
{
    private readonly string _configurationFolderPath;
    private readonly ILogger<UpdateConfigurationService> _logger;

    public UpdateConfigurationService(IConfiguration configuration, ILogger<UpdateConfigurationService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        this._configurationFolderPath = configuration["UpdateConfigurationsPath"] ?? "/app/config/updates";
        this._logger = logger;

        // Ensure the configuration folder exists
        if (!Directory.Exists(this._configurationFolderPath))
        {
            Directory.CreateDirectory(this._configurationFolderPath);
            this._logger.LogInformation("Created update configurations folder: {Path}", this._configurationFolderPath);
        }
    }

    public async Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync()
    {
        var configurations = new List<UpdateConfiguration>();

        if (!Directory.Exists(this._configurationFolderPath))
        {
            this._logger.LogWarning("Update configurations folder does not exist: {Path}", this._configurationFolderPath);
            return configurations;
        }

        var files = Directory.GetFiles(this._configurationFolderPath, "*.*");

        foreach (var file in files)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(file);

                if (lines.Length < 3)
                {
                    this._logger.LogWarning("Configuration file {File} does not have enough lines (minimum 3 required)", file);
                    continue;
                }

                var queryTitle = lines[0].Trim().TrimStart('-').Trim();
                var emailAddress = lines[1].Trim().TrimStart('-').Trim();
                var sqlQuery = string.Join(Environment.NewLine, lines.Skip(2)).Trim();

                if (string.IsNullOrWhiteSpace(emailAddress) || string.IsNullOrWhiteSpace(queryTitle) || string.IsNullOrWhiteSpace(sqlQuery))
                {
                    this._logger.LogWarning("Configuration file {File} has empty required fields", file);
                    continue;
                }

                configurations.Add(new UpdateConfiguration
                {
                    EmailReceiverAddress = emailAddress,
                    QueryTitle = queryTitle,
                    SqlQuery = sqlQuery,
                    FilePath = file
                });

                this._logger.LogInformation("Loaded update configuration from {File}: {Title}", file, queryTitle);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error reading configuration file {File}", file);
            }
        }

        return configurations;
    }
}