using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ravuno.WebAPI.Settings;

namespace Ravuno.WebAPI.Services;

public partial class FetchAndSendHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FetchAndSendHostedService> _logger;
    private readonly FetchAndSendSettings _settings;

    public FetchAndSendHostedService(
        IServiceProvider serviceProvider,
        ILogger<FetchAndSendHostedService> logger,
        IOptions<FetchAndSendSettings> settings
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        this._serviceProvider = serviceProvider;
        this._logger = logger;
        this._settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation(
            "FetchAndSendHostedService starting with interval: {Interval}, detailed every: {DetailedEvery} runs",
            this._settings.FetchInterval,
            this._settings.FetchDetailedEvery
        );

        using var generalTimer = new PeriodicTimer(this._settings.FetchInterval);
        await this.RunGeneralFetchAsync(generalTimer, stoppingToken);
        this._logger.LogInformation("FetchAndSendHostedService is stopped");
    }

    private async Task RunGeneralFetchAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        await this.WaitUntilFetchIntervalExpiredAfterLastRun();

        var runCounter = 0;
        do
        {
            var isDetailed =
                this._settings.FetchDetailedEvery > 0
                && runCounter % this._settings.FetchDetailedEvery == 0;
            var nextRunTime = DateTime.UtcNow.Add(this._settings.FetchInterval);

            this._logger.LogInformation(
                "Starting fetch operation #{RunNumber} (detailed: {IsDetailed}). Next execution expected at: {NextRun}",
                runCounter + 1,
                isDetailed,
                nextRunTime
            );

            try
            {
                using var scope = this._serviceProvider.CreateScope();
                var fetchAndSendService =
                    scope.ServiceProvider.GetRequiredService<FetchAndSendService>();
                await fetchAndSendService.ProcessFetchAndSendAsync(
                    detailed: isDetailed,
                    stoppingToken
                );

                this._logger.LogInformation(
                    "Fetch operation #{RunNumber} completed successfully (detailed: {IsDetailed}). Next run expected at {NextRun}",
                    runCounter + 1,
                    isDetailed,
                    nextRunTime
                );
            }
            catch (Exception ex)
            {
                this._logger.LogError(
                    ex,
                    "Error occurred during fetch operation #{RunNumber} (detailed: {IsDetailed}). Next run expected at {NextRun}",
                    runCounter + 1,
                    isDetailed,
                    nextRunTime
                );
            }

            runCounter++;
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task WaitUntilFetchIntervalExpiredAfterLastRun()
    {
        using var scope = this._serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataStorage.DataStorageContext>();

        var latestFetch = await context
            .FetchHistories.OrderByDescending(f => f.ExecutionStartTime)
            .FirstOrDefaultAsync();

        if (latestFetch != null)
        {
            var nextRunTime = latestFetch.ExecutionStartTime.Add(this._settings.FetchInterval);
            var delay = nextRunTime - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                this._logger.LogInformation(
                    "Initial startup wait: Delaying for {Delay} until fetch interval expires. Last run: {LastRun}, scheduled next run: {NextRun}",
                    delay,
                    latestFetch.ExecutionStartTime,
                    nextRunTime
                );
                await Task.Delay(delay);
                this._logger.LogInformation(
                    "Initial startup wait completed, starting normal execution cycle"
                );
            }
            else
            {
                this._logger.LogInformation(
                    "No initial startup wait needed. Last run was at {LastRun}, which was {TimeAgo} ago (more than configured interval of {Interval})",
                    latestFetch.ExecutionStartTime,
                    DateTime.UtcNow - latestFetch.ExecutionStartTime,
                    this._settings.FetchInterval
                );
            }
        }
        else
        {
            this._logger.LogInformation(
                "No previous fetch history found, starting execution immediately"
            );
        }
    }
}
