using Microsoft.EntityFrameworkCore;
using Ravuno.Core.Contracts;
using Ravuno.Core.Extensions;
using Ravuno.DataStorage;
using Ravuno.DataStorage.Models;
using Ravuno.Email.Services.Contracts;
using Ravuno.Fetcher.DntActivities.Services.Contracts;
using Ravuno.Fetcher.Tekna.Services.Contracts;
using Ravuno.WebAPI.Extensions;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Services;

public class FetchAndSendService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FetchAndSendService> _logger;
    private readonly DataStorageContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IUpdateConfigurationService _configService;
    private static readonly SemaphoreSlim _executionSemaphore = new(1, 1);

    private static readonly Dictionary<ItemSource, Type> ItemFetcher = new()
    {
        [ItemSource.Tekna] = typeof(ITeknaFetchService),
        [ItemSource.DntActivities] = typeof(IDntActivityFetchService),
    };

    public FetchAndSendService(
        IServiceProvider serviceProvider,
        ILogger<FetchAndSendService> logger,
        DataStorageContext dbContext,
        IEmailService emailService,
        IUpdateConfigurationService configService
    )
    {
        this._serviceProvider = serviceProvider;
        this._logger = logger;
        this._dbContext = dbContext;
        this._emailService = emailService;
        this._configService = configService;
    }

    public async Task ProcessFetchAndSendAsync(bool detailed, CancellationToken cancellationToken)
    {
        await _executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            this._logger.LogInformation("Starting fetch and send process");

            // Get update configurations
            var updateConfigs = await this._configService.GetUpdateConfigurationsAsync();
            this._logger.LogInformation(
                "Loaded {Count} update configurations",
                updateConfigs.Count
            );

            // Execute "before" queries for all configurations
            var beforeResultsDict = new Dictionary<string, List<Item>>();
            foreach (var config in updateConfigs)
            {
                var beforeResults = await this.ExecuteSqlQueryAsync(
                    config.SqlQuery,
                    cancellationToken
                );
                beforeResultsDict[config.QueryTitle] = beforeResults;
                this._logger.LogInformation(
                    "Query '{QueryTitle}' returned {Count} items before update",
                    config.QueryTitle,
                    beforeResults.Count
                );
            }

            var enabledFetchers = ItemFetcher
                .Select(kvp =>
                    (kvp.Key, this._serviceProvider.GetService(kvp.Value) as IFetcherService)
                )
                .Where(fetcher => fetcher.Item2 != null && fetcher.Item2.IsEnabled)
                .ToDictionary(fetcher => fetcher.Key, fetcher => fetcher.Item2!);

            var allItems = new List<Item>();
            var fetchHistoryTrackerItems = new Dictionary<ItemSource, FetchHistory>();
            foreach (var (source, fetcherService) in enabledFetchers)
            {
                try
                {
                    this._logger.LogInformation(
                        "Preparing to fetch items from source: {Source}",
                        source
                    );

                    var existingItems = await this
                        ._dbContext.Items.Where(i => i.Source == source)
                        .ToListAsync(cancellationToken);

                    this._logger.LogInformation("Fetching data from {Source}", source);
                    var startTime = DateTime.UtcNow;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    var items = await fetcherService.FetchAsync(
                        existingItems,
                        detailed,
                        cancellationToken
                    );

                    stopwatch.Stop();
                    this._logger.LogInformation(
                        "Fetched {Count} items from {Source}",
                        items.Count,
                        source
                    );

                    var fetchHistory = new FetchHistory
                    {
                        Source = source,
                        ExecutionStartTime = startTime,
                        ExecutionDuration = stopwatch.Elapsed,
                        ItemsRetrieved = items.Count,
                        NewItems = 0,
                        UpdatedItems = 0,
                        IsDetailed = detailed,
                    };
                    this._dbContext.FetchHistories.Add(fetchHistory);
                    this._dbContext.Entry(fetchHistory).State = EntityState.Added;
                    fetchHistoryTrackerItems.Add(source, fetchHistory);

                    allItems.AddRange(items);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Error fetching items from source {Source}", source);
                }
            }

            var allFetchedItems = allItems;

            // Compare and update database, tracking stats per source
            var (newItems, updatedItems) = await this.CompareAndUpdateItemsAsync(
                allFetchedItems,
                cancellationToken
            );

            foreach (var (source, _) in enabledFetchers)
            {
                if (!fetchHistoryTrackerItems.ContainsKey(source))
                {
                    continue;
                }

                var fetchHistory = fetchHistoryTrackerItems[source];
                fetchHistory.NewItems = newItems.Count(i => i.Source == source);
                fetchHistory.UpdatedItems = updatedItems.Count(i => i.Source == source);
            }

            await this._dbContext.SaveChangesAsync(cancellationToken);

            this._logger.LogInformation(
                "Found {NewCount} new items and {UpdatedCount} updated items total",
                newItems.Count,
                updatedItems.Count
            );

            // Process each update configuration
            foreach (var config in updateConfigs)
            {
                try
                {
                    await this.ProcessUpdateConfigurationAsync(
                        config,
                        beforeResultsDict[config.QueryTitle],
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    this._logger.LogError(
                        ex,
                        "Error processing update configuration {QueryTitle}",
                        config.QueryTitle
                    );
                }
            }

            this._logger.LogInformation("Fetch and send process completed");
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    private async Task ProcessUpdateConfigurationAsync(
        Models.UpdateConfiguration config,
        List<Item> beforeResults,
        CancellationToken cancellationToken
    )
    {
        this._logger.LogInformation("Processing configuration: {QueryTitle}", config.QueryTitle);

        this._logger.LogInformation(
            "Query returned {Count} items before update",
            beforeResults.Count
        );

        // Execute SQL query after updates
        var afterResults = await this.ExecuteSqlQueryAsync(config.SqlQuery, cancellationToken);
        this._logger.LogInformation(
            "Query returned {Count} items after update",
            afterResults.Count
        );

        // Check if this query has been sent before
        var lastSendHistory = await this
            ._dbContext.SendUpdateHistories.Where(sh =>
                sh.QueryTitle == config.QueryTitle
                && config.EmailReceiverAddresses.Contains(sh.EmailReceiverAddress)
            )
            .OrderByDescending(sh => sh.SentAt)
            .FirstOrDefaultAsync(cancellationToken);

        List<Item> newDelta;
        List<Item> updatedDelta;

        if (
            lastSendHistory == null
            && beforeResults.Count == afterResults.Count
            && beforeResults.TrueForAll(before =>
                afterResults.Exists(after => before.IsEqualTo(after))
            )
        )
        {
            // Never sent before and before/after match - send all as new
            this._logger.LogInformation(
                "First time sending for {QueryTitle} with no changes detected - sending all {Count} items as new",
                config.QueryTitle,
                afterResults.Count
            );
            newDelta = afterResults;
            updatedDelta = [];
        }
        else
        {
            // Calculate deltas normally
            newDelta =
            [
                .. afterResults.Where(after =>
                    !beforeResults.Exists(before => before.IsEqualTo(after))
                ),
            ];

            updatedDelta =
            [
                .. afterResults.Where(after =>
                    beforeResults.Exists(before =>
                        before.IsEqualTo(after) && !before.IsFullyEqualTo(after)
                    )
                ),
            ];

            this._logger.LogInformation(
                "Delta: {NewCount} new, {UpdatedCount} updated",
                newDelta.Count,
                updatedDelta.Count
            );
        }

        // Send email if there are changes
        if (newDelta.Count > 0 || updatedDelta.Count > 0)
        {
            foreach (var receiver in config.EmailReceiverAddresses)
            {
                try
                {
                    await this._emailService.SendItemUpdateEmailAsync(
                        receiver,
                        config.QueryTitle,
                        newDelta,
                        updatedDelta
                    );
                    this._logger.LogInformation(
                        "Email sent to {Email} for {QueryTitle}",
                        receiver,
                        config.QueryTitle
                    );

                    var sendHistory = new SendUpdateHistory
                    {
                        QueryTitle = config.QueryTitle,
                        EmailReceiverAddress = receiver,
                        SentAt = DateTime.UtcNow,
                        NewItemsCount = newDelta.Count,
                        UpdatedItemsCount = updatedDelta.Count,
                    };
                    this._dbContext.SendUpdateHistories.Add(sendHistory);
                    await this._dbContext.SaveChangesAsync(cancellationToken);
                    this._logger.LogInformation(
                        "Recorded send history for {QueryTitle} and {Email}: {NewCount} new, {UpdatedCount} updated",
                        config.QueryTitle,
                        receiver,
                        newDelta.Count,
                        updatedDelta.Count
                    );
                }
                catch (Exception ex)
                {
                    this._logger.LogError(
                        ex,
                        "Error sending email or recording history for {QueryTitle} to {Email}",
                        config.QueryTitle,
                        receiver
                    );
                }
            }
        }
        else
        {
            this._logger.LogInformation(
                "No changes detected for {QueryTitle}, skipping email",
                config.QueryTitle
            );
        }
    }

    private async Task<(List<Item> newItems, List<Item> updatedItems)> CompareAndUpdateItemsAsync(
        List<Item> fetchedItems,
        CancellationToken cancellationToken
    )
    {
        var newItems = new List<Item>();
        var updatedItems = new List<Item>();

        // Load all existing items for the sources we're updating in one query
        var sources = fetchedItems.Select(i => i.Source).Distinct().ToList();
        var existingItemsDict = await this
            ._dbContext.Items.Where(i => sources.Contains(i.Source))
            .ToDictionaryAsync(i => (i.Source, i.SourceId), cancellationToken);

        foreach (var fetchedItem in fetchedItems)
        {
            // Lookup existing item from the dictionary
            var key = (fetchedItem.Source, fetchedItem.SourceId);
            var existingItem = existingItemsDict.GetValueOrDefault(key);

            if (existingItem == null)
            {
                // New item
                this._dbContext.Items.Add(fetchedItem);
                newItems.Add(fetchedItem);
            }
            else
            {
                // Check if properties need updating
                var hasChanges = false;

                if (existingItem.Price != fetchedItem.Price)
                {
                    existingItem.Price = fetchedItem.Price;
                    hasChanges = true;
                }

                if (existingItem.Description != fetchedItem.Description)
                {
                    existingItem.Description = fetchedItem.Description;
                    hasChanges = true;
                }

                if (existingItem.Location != fetchedItem.Location)
                {
                    existingItem.Location = fetchedItem.Location;
                    hasChanges = true;
                }

                if (existingItem.EnrollmentDeadline != fetchedItem.EnrollmentDeadline)
                {
                    existingItem.EnrollmentDeadline = fetchedItem.EnrollmentDeadline;
                    hasChanges = true;
                }

                if (existingItem.Url != fetchedItem.Url)
                {
                    existingItem.Url = fetchedItem.Url;
                    hasChanges = true;
                }

                if (
                    !(
                        existingItem.Tags?.SequenceEqual(fetchedItem.Tags ?? [])
                        ?? (fetchedItem.Tags == null)
                    )
                )
                {
                    existingItem.Tags = fetchedItem.Tags;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    existingItem.RetrievedAt = fetchedItem.RetrievedAt;
                    existingItem.RawData = fetchedItem.RawData;
                    updatedItems.Add(existingItem);
                }
            }
        }

        await this._dbContext.SaveChangesAsync(cancellationToken);
        return (newItems, updatedItems);
    }

    private async Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken
    )
    {
        return await this._configService.ExecuteSqlQueryAsync(sqlQuery, cancellationToken);
    }
}
