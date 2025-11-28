using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ravuno.DataStorage;
using Ravuno.WebAPI.Settings;

namespace Ravuno.WebAPI.Services;

public class ItemCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ItemCleanupService> _logger;
    private readonly CleanupSettings _settings;

    public ItemCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ItemCleanupService> logger,
        IOptions<CleanupSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this._serviceProvider = serviceProvider;
        this._logger = logger;
        this._settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation(
            "ItemCleanupService is starting. Cleanup interval: {Interval}, Retention time span: {Retention}",
            this._settings.CleanUpInterval,
            this._settings.HistoricRetentionTimeSpan);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.PerformCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error occurred during cleanup operation");
            }

            this._logger.LogInformation("Next cleanup scheduled in {Interval}", this._settings.CleanUpInterval);
            await Task.Delay(this._settings.CleanUpInterval, stoppingToken);
        }

        this._logger.LogInformation("ItemCleanupService is stopping");
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = this._serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataStorageContext>();

        var cutoffDate = DateTime.UtcNow.Subtract(this._settings.HistoricRetentionTimeSpan);

        this._logger.LogInformation(
            "Starting cleanup of items with EventStartDateTime and EventEndDateTime before {CutoffDate}",
            cutoffDate);

        var itemsToDelete = await dbContext.Items
            .Where(i => i.EventStartDateTime < cutoffDate && i.EventEndDateTime < cutoffDate)
            .ToListAsync(cancellationToken);

        if (itemsToDelete.Count > 0)
        {
            dbContext.Items.RemoveRange(itemsToDelete);
            await dbContext.SaveChangesAsync(cancellationToken);

            this._logger.LogInformation(
                "Cleanup completed. Removed {Count} items older than {CutoffDate}",
                itemsToDelete.Count,
                cutoffDate);
        }
        else
        {
            this._logger.LogInformation("Cleanup completed. No items to remove.");
        }
    }
}
