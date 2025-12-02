using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;
using Ravuno.DataStorage.Models;
using Ravuno.Email.Services.Contracts;
using Ravuno.Fetcher.Contracts;
using Ravuno.Fetcher.DntActivities.Services.Contracts;
using Ravuno.Fetcher.Tekna.Services.Contracts;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Services;

public partial class FetchAndSendService
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
        IUpdateConfigurationService configService)
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
            this._logger.LogInformation("Loaded {Count} update configurations", updateConfigs.Count);

            // Execute "before" queries for all configurations
            var beforeResultsDict = new Dictionary<string, List<Item>>();
            foreach (var config in updateConfigs)
            {
                var beforeResults = await this.ExecuteSqlQueryAsync(config.SqlQuery, cancellationToken);
                beforeResultsDict[config.QueryTitle] = beforeResults;
                this._logger.LogInformation("Query '{QueryTitle}' returned {Count} items before update",
                    config.QueryTitle, beforeResults.Count);
            }

            var enabledFetchers = ItemFetcher.Select(kvp => (kvp.Key, this._serviceProvider.GetService(kvp.Value) as IFetcherService))
                    .Where(fetcher => fetcher.Item2 != null && fetcher.Item2.IsEnabled)
                    .ToDictionary(fetcher => fetcher.Key, fetcher => fetcher.Item2!);

            var allItems = new List<Item>();
            var fetchHistoryTrackerItems = new Dictionary<ItemSource, FetchHistory>();
            foreach (var (source, fetcherService) in enabledFetchers)
            {
                try
                {
                    this._logger.LogInformation("Preparing to fetch items from source: {Source}", source);

                    var existingItems = await this._dbContext.Items
                        .Where(i => i.Source == source)
                        .ToListAsync(cancellationToken);

                    this._logger.LogInformation("Fetching data from {Source}", source);
                    var startTime = DateTime.UtcNow;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    var items = await fetcherService.FetchAsync(existingItems, detailed, cancellationToken);

                    stopwatch.Stop();
                    this._logger.LogInformation("Fetched {Count} items from {Source}", items.Count, source);

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
            var (newItems, updatedItems) = await this.CompareAndUpdateItemsAsync(allFetchedItems, cancellationToken);

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

            this._logger.LogInformation("Found {NewCount} new items and {UpdatedCount} updated items total",
                newItems.Count, updatedItems.Count);

            // Process each update configuration
            foreach (var config in updateConfigs)
            {
                try
                {
                    await this.ProcessUpdateConfigurationAsync(
                        config,
                        beforeResultsDict[config.QueryTitle],
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Error processing update configuration {QueryTitle}", config.QueryTitle);
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
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Processing configuration: {QueryTitle}", config.QueryTitle);

        this._logger.LogInformation("Query returned {Count} items before update", beforeResults.Count);

        // Execute SQL query after updates
        var afterResults = await this.ExecuteSqlQueryAsync(config.SqlQuery, cancellationToken);
        this._logger.LogInformation("Query returned {Count} items after update", afterResults.Count);

        // Check if this query has been sent before
        var lastSendHistory = await this._dbContext.SendUpdateHistories
            .Where(sh => sh.QueryTitle == config.QueryTitle && sh.EmailReceiverAddress == config.EmailReceiverAddress)
            .OrderByDescending(sh => sh.SentAt)
            .FirstOrDefaultAsync(cancellationToken);

        List<Item> newDelta;
        List<Item> updatedDelta;

        if (lastSendHistory == null && beforeResults.Count == afterResults.Count &&
            beforeResults.TrueForAll(before => afterResults.Exists(after => AreItemsEqual(before, after))))
        {
            // Never sent before and before/after match - send all as new
            this._logger.LogInformation("First time sending for {QueryTitle} with no changes detected - sending all {Count} items as new",
                config.QueryTitle, afterResults.Count);
            newDelta = afterResults;
            updatedDelta = [];
        }
        else
        {
            // Calculate deltas normally
            newDelta = [.. afterResults
                .Where(after => !beforeResults.Exists(before =>
                    AreItemsEqual(before, after)))];

            updatedDelta = [.. afterResults
                .Where(after => beforeResults.Exists(before =>
                    AreItemsEqual(before, after) && !AreItemsFullyEqual(before, after)))];

            this._logger.LogInformation("Delta: {NewCount} new, {UpdatedCount} updated",
                newDelta.Count, updatedDelta.Count);
        }

        // Send email if there are changes
        if (newDelta.Count > 0 || updatedDelta.Count > 0)
        {
            var emailBody = this.BuildEmailBody([.. newDelta.OrderBy(item => item.EventStartDateTime)], [.. updatedDelta.OrderBy(item => item.EventStartDateTime)]);
            await this._emailService.SendEmailAsync(config.EmailReceiverAddress, $"[Ravuno] {config.QueryTitle} ({newDelta.Count} new, {updatedDelta.Count} updated)", emailBody, isHtml: true);
            this._logger.LogInformation("Email sent to {Email} for {QueryTitle}",
                config.EmailReceiverAddress, config.QueryTitle);

            // Record send history
            var sendHistory = new SendUpdateHistory
            {
                QueryTitle = config.QueryTitle,
                EmailReceiverAddress = config.EmailReceiverAddress,
                SentAt = DateTime.UtcNow,
                NewItemsCount = newDelta.Count,
                UpdatedItemsCount = updatedDelta.Count
            };
            this._dbContext.SendUpdateHistories.Add(sendHistory);
            await this._dbContext.SaveChangesAsync(cancellationToken);

            this._logger.LogInformation("Recorded send history for {QueryTitle}: {NewCount} new, {UpdatedCount} updated",
                config.QueryTitle, newDelta.Count, updatedDelta.Count);
        }
        else
        {
            this._logger.LogInformation("No changes detected for {QueryTitle}, skipping email", config.QueryTitle);
        }
    }

    private async Task<(List<Item> newItems, List<Item> updatedItems)> CompareAndUpdateItemsAsync(
        List<Item> fetchedItems,
        CancellationToken cancellationToken)
    {
        var newItems = new List<Item>();
        var updatedItems = new List<Item>();

        foreach (var fetchedItem in fetchedItems)
        {
            // Find existing item by source, title, and event date
            var existingItem = await this._dbContext.Items
                .FirstOrDefaultAsync(i =>
                    i.Source == fetchedItem.Source &&
                    i.Title == fetchedItem.Title &&
                    i.EventStartDateTime == fetchedItem.EventStartDateTime &&
                    i.EventEndDateTime == fetchedItem.EventEndDateTime,
                    cancellationToken);

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
        CancellationToken cancellationToken)
    {
        return await this._dbContext.Database.SqlQueryRaw<Item>(sqlQuery)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private static bool AreItemsEqual(Item item1, Item item2)
    {
        return item1.Source == item2.Source &&
               item1.Title == item2.Title &&
               item1.EventEndDateTime.Date == item2.EventEndDateTime.Date &&
               item1.EventStartDateTime.Date == item2.EventStartDateTime.Date &&
               item1.SourceId == item2.SourceId;
    }

    private static bool AreItemsFullyEqual(Item item1, Item item2)
    {
        return AreItemsEqual(item1, item2) &&
               item1.EventStartDateTime == item2.EventStartDateTime &&
               item1.EventEndDateTime == item2.EventEndDateTime &&
               item1.Price == item2.Price &&
               item1.Description == item2.Description &&
               item1.Location == item2.Location &&
               item1.EnrollmentDeadline.Date == item2.EnrollmentDeadline.Date &&
               item1.Url == item2.Url;
    }

    private string StripHtmlTags(string html)
    {
        try
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            var htmlWithLinebreaks = HtmlLineBreaksRegex().Replace(html.Replace("\r", "").Replace("\n", ""), "\n");
            var text = HtmlTagsRegex().Replace(htmlWithLinebreaks, string.Empty);
            text = System.Net.WebUtility.HtmlDecode(text);
            return MultipleLineBreaksRegex().Replace(text, "\n").Trim();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to strip HTML tags from text.");
            return html;
        }
    }

    private string BuildEmailBody(List<Item> newItems, List<Item> updatedItems)
    {
        void RenderTable(StringBuilder sb, string heading, List<Item> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"<h2>{heading}</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th style=\"min-width: 20em;\">Title</th>");
            sb.AppendLine("<th style=\"min-width: 5em;\">When?<br/>(Enrollment Deadline)</th>");
            sb.AppendLine("<th style=\"min-width: 5em;\">Location</th>");
            sb.AppendLine("<th style=\"min-width: 5em;\">Price</th>");
            sb.AppendLine("</tr>");

            foreach (var item in items)
            {
                var description = this.StripHtmlTags(item.Description ?? "");
                if (description.Length > 2500)
                {
                    description = string.Concat(description.AsSpan(0, 2500), "...");
                }

                description = description.Replace("\n", "<br/>");
                var tags = item.Tags != null ? string.Join(", ", item.Tags) : "";

                sb.AppendLine("<tr>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td><a href=\"{item.Url}\">{item.Title}</a></td>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td>{item.EventStartDateTime:ddd, yyyy-MM-dd HH:mm} to<br/>{item.EventEndDateTime:ddd, yyyy-MM-dd HH:mm}<br/>({item.EnrollmentDeadline:ddd, yyyy-MM-dd HH:mm})</td>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td>{item.Location?.Replace("\n", "<br/>")}</td>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td>{item.Price?.Replace("\n", "<br/>")}</td>");
                sb.AppendLine("</tr>");
                sb.AppendLine("<tr>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td colspan=\"5\" class=\"description\">{description}<br/>{tags}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
        }

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Trebuchet MS', Arial, sans-serif; }");
        sb.AppendLine("h2 { color: #333; }");
        sb.AppendLine("table { border-collapse: collapse; min-width: 100%; margin-bottom: 20px; }");
        sb.AppendLine("th, td { border: 1px solid #ddd; padding: 2px 6px 2px 6px; text-align: left; vertical-align: top; }");
        sb.AppendLine("th { background-color: #4CAF50; color: white; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
        sb.AppendLine("a { color: #1a73e8; text-decoration: none; }");
        sb.AppendLine(".description { font-size: 0.9em; padding-bottom: 8px; color: #555; }");
        sb.AppendLine(".tags { font-size: 0.85em; margin-top: 4px; color: #777; font-style: italic; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<p><em>All timestamps are given as provided by the data sources. Usually local time.</em></p>");

        RenderTable(sb, "New Items", newItems);
        RenderTable(sb, "Updated Items", updatedItems);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    [GeneratedRegex(@"<.*?>")]
    private static partial Regex HtmlTagsRegex();

    [GeneratedRegex(@"<(/?)(br|p)[^>]*>")]
    private static partial Regex HtmlLineBreaksRegex();

    [GeneratedRegex(@"(\n\s*)+")]
    private static partial Regex MultipleLineBreaksRegex();
}