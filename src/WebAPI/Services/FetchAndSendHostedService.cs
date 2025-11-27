using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DataStorage;
using DataStorage.Models;
using DntActivities.Services.Contracts;
using Email.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Tekna.Services.Contracts;
using WebAPI.Services.Contracts;

namespace WebAPI.Services;

public partial class FetchAndSendHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FetchAndSendHostedService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _fetchThreshold;

    public FetchAndSendHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<FetchAndSendHostedService> logger)
    {
        this._serviceProvider = serviceProvider;
        this._logger = logger;

        var intervalHours = configuration.GetValue("FetchAndSendService:IntervalHours", 24);
        this._interval = TimeSpan.FromHours(intervalHours);

        var fetchThresholdHours = configuration.GetValue("FetchAndSendService:FetchThresholdHours", 24);
        this._fetchThreshold = TimeSpan.FromHours(fetchThresholdHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("FetchAndSendHostedService is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this.ProcessFetchAndSendAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error occurred during fetch and send operation");
            }

            this._logger.LogInformation("Next run scheduled in {Interval}", this._interval);
            await Task.Delay(this._interval, stoppingToken);
        }

        this._logger.LogInformation("FetchAndSendHostedService is stopping");
    }

    private async Task ProcessFetchAndSendAsync(CancellationToken cancellationToken)
    {
        using var scope = this._serviceProvider.CreateScope();

        var teknaService = scope.ServiceProvider.GetRequiredService<ITeknaFetchService>();
        var dntService = scope.ServiceProvider.GetRequiredService<IDntActivityFetchService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataStorageContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var configService = scope.ServiceProvider.GetRequiredService<IUpdateConfigurationService>();

        this._logger.LogInformation("Starting fetch and send process");

        // Get update configurations
        var updateConfigs = await configService.GetUpdateConfigurationsAsync();
        this._logger.LogInformation("Loaded {Count} update configurations", updateConfigs.Count);

        // Execute "before" queries for all configurations
        var beforeResultsDict = new Dictionary<string, List<Item>>();
        foreach (var config in updateConfigs)
        {
            var beforeResults = await ExecuteSqlQueryAsync(dbContext, config.SqlQuery, cancellationToken);
            beforeResultsDict[config.QueryTitle] = beforeResults;
            this._logger.LogInformation("Query '{QueryTitle}' returned {Count} items before update",
                config.QueryTitle, beforeResults.Count);
        }

        // Check if we should fetch from Tekna
        var shouldFetchTekna = await this.ShouldFetchSourceAsync(dbContext, ItemSource.Tekna, cancellationToken);
        List<Item> teknaItems = [];
        FetchHistory? teknaFetchHistory = null;
        if (shouldFetchTekna)
        {
            this._logger.LogInformation("Fetching data from Tekna");
            var startTime = DateTime.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            teknaItems = await teknaService.FetchItemsAsync();

            stopwatch.Stop();
            this._logger.LogInformation("Fetched {Count} items from Tekna", teknaItems.Count);

            teknaFetchHistory = new FetchHistory
            {
                Source = ItemSource.Tekna,
                ExecutionStartTime = startTime,
                ExecutionDuration = stopwatch.Elapsed,
                ItemsRetrieved = teknaItems.Count,
                NewItems = 0,
                UpdatedItems = 0
            };
            dbContext.FetchHistories.Add(teknaFetchHistory);
        }
        else
        {
            this._logger.LogInformation("Skipping Tekna fetch - last fetch was within threshold");
        }

        // Check if we should fetch from DNT Activities
        var shouldFetchDnt = await this.ShouldFetchSourceAsync(dbContext, ItemSource.DntActivities, cancellationToken);
        List<Item> dntItems = [];
        FetchHistory? dntFetchHistory = null;
        if (shouldFetchDnt)
        {
            this._logger.LogInformation("Fetching data from DNT Activities");
            var startTime = DateTime.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            dntItems = await dntService.FetchItemsAsync();

            stopwatch.Stop();
            this._logger.LogInformation("Fetched {Count} items from DNT Activities", dntItems.Count);

            dntFetchHistory = new FetchHistory
            {
                Source = ItemSource.DntActivities,
                ExecutionStartTime = startTime,
                ExecutionDuration = stopwatch.Elapsed,
                ItemsRetrieved = dntItems.Count,
                NewItems = 0,
                UpdatedItems = 0
            };
            dbContext.FetchHistories.Add(dntFetchHistory);
        }
        else
        {
            this._logger.LogInformation("Skipping DNT Activities fetch - last fetch was within threshold");
        }

        var allFetchedItems = teknaItems.Concat(dntItems).ToList();

        // Compare and update database, tracking stats per source
        var (newItems, updatedItems) = await CompareAndUpdateItemsAsync(dbContext, allFetchedItems, cancellationToken);

        // Update fetch histories with actual new/updated counts
        if (teknaFetchHistory != null)
        {
            teknaFetchHistory.NewItems = newItems.Count(i => i.Source == ItemSource.Tekna);
            teknaFetchHistory.UpdatedItems = updatedItems.Count(i => i.Source == ItemSource.Tekna);
        }

        if (dntFetchHistory != null)
        {
            dntFetchHistory.NewItems = newItems.Count(i => i.Source == ItemSource.DntActivities);
            dntFetchHistory.UpdatedItems = updatedItems.Count(i => i.Source == ItemSource.DntActivities);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        this._logger.LogInformation("Found {NewCount} new items and {UpdatedCount} updated items total",
            newItems.Count, updatedItems.Count);

        // Process each update configuration
        foreach (var config in updateConfigs)
        {
            try
            {
                await this.ProcessUpdateConfigurationAsync(
                    dbContext,
                    emailService,
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

    private async Task<bool> ShouldFetchSourceAsync(
        DataStorageContext dbContext,
        ItemSource source,
        CancellationToken cancellationToken)
    {
        var lastFetch = await dbContext.FetchHistories
            .Where(fh => fh.Source == source)
            .OrderByDescending(fh => fh.ExecutionStartTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastFetch == null)
        {
            return true; // Never fetched before
        }

        var timeSinceLastFetch = DateTime.UtcNow - lastFetch.ExecutionStartTime;
        return timeSinceLastFetch >= this._fetchThreshold;
    }

    private async Task ProcessUpdateConfigurationAsync(
        DataStorageContext dbContext,
        IEmailService emailService,
        Models.UpdateConfiguration config,
        List<Item> beforeResults,
        CancellationToken cancellationToken)
    {
        this._logger.LogInformation("Processing configuration: {QueryTitle}", config.QueryTitle);

        this._logger.LogInformation("Query returned {Count} items before update", beforeResults.Count);

        // Execute SQL query after updates
        var afterResults = await ExecuteSqlQueryAsync(dbContext, config.SqlQuery, cancellationToken);
        this._logger.LogInformation("Query returned {Count} items after update", afterResults.Count);

        // Check if this query has been sent before
        var lastSendHistory = await dbContext.SendUpdateHistories
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
            await emailService.SendEmailAsync(config.EmailReceiverAddress, $"[Ravuno] {config.QueryTitle} ({newDelta.Count} new, {updatedDelta.Count} updated)", emailBody, isHtml: true);
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
            dbContext.SendUpdateHistories.Add(sendHistory);
            await dbContext.SaveChangesAsync(cancellationToken);

            this._logger.LogInformation("Recorded send history for {QueryTitle}: {NewCount} new, {UpdatedCount} updated",
                config.QueryTitle, newDelta.Count, updatedDelta.Count);
        }
        else
        {
            this._logger.LogInformation("No changes detected for {QueryTitle}, skipping email", config.QueryTitle);
        }
    }

    private static async Task<(List<Item> newItems, List<Item> updatedItems)> CompareAndUpdateItemsAsync(
        DataStorageContext dbContext,
        List<Item> fetchedItems,
        CancellationToken cancellationToken)
    {
        var newItems = new List<Item>();
        var updatedItems = new List<Item>();

        foreach (var fetchedItem in fetchedItems)
        {
            // Find existing item by source, title, and event date
            var existingItem = await dbContext.Items
                .FirstOrDefaultAsync(i =>
                    i.Source == fetchedItem.Source &&
                    i.Title == fetchedItem.Title &&
                    i.EventStartDateTime == fetchedItem.EventStartDateTime &&
                    i.EventEndDateTime == fetchedItem.EventEndDateTime,
                    cancellationToken);

            if (existingItem == null)
            {
                // New item
                dbContext.Items.Add(fetchedItem);
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

        await dbContext.SaveChangesAsync(cancellationToken);
        return (newItems, updatedItems);
    }

    private static async Task<List<Item>> ExecuteSqlQueryAsync(
        DataStorageContext dbContext,
        string sqlQuery,
        CancellationToken cancellationToken)
    {
        var items = new List<Item>();

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sqlQuery;

        if (command.Connection?.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new Item
            {
                Source = Enum.Parse<ItemSource>(reader.GetString(reader.GetOrdinal("Source"))),
                RetrievedAt = reader.GetDateTime(reader.GetOrdinal("RetrievedAt")),
                EventStartDateTime = reader.GetDateTime(reader.GetOrdinal("EventStartDateTime")),
                EventEndDateTime = reader.GetDateTime(reader.GetOrdinal("EventEndDateTime")),
                Title = await reader.IsDBNullAsync(reader.GetOrdinal("Title"), cancellationToken) ? null : reader.GetString(reader.GetOrdinal("Title")),
                Description = await reader.IsDBNullAsync(reader.GetOrdinal("Description"), cancellationToken) ? null : reader.GetString(reader.GetOrdinal("Description")),
                Location = await reader.IsDBNullAsync(reader.GetOrdinal("Location"), cancellationToken) ? null : reader.GetString(reader.GetOrdinal("Location")),
                Url = await reader.IsDBNullAsync(reader.GetOrdinal("Url"), cancellationToken) ? null : reader.GetString(reader.GetOrdinal("Url")),
                Price = await reader.IsDBNullAsync(reader.GetOrdinal("Price"), cancellationToken) ? null : reader.GetString(reader.GetOrdinal("Price")),
                EnrollmentDeadline = reader.GetDateTime(reader.GetOrdinal("EnrollmentDeadline")),
                RawData = await reader.IsDBNullAsync(reader.GetOrdinal("RawData"), cancellationToken) ? null : reader.GetString(reader.GetOrdinal("RawData"))
            };

            items.Add(item);
        }

        return items;
    }

    private static bool AreItemsEqual(Item item1, Item item2)
    {
        return item1.Source == item2.Source &&
               item1.Title == item2.Title &&
               item1.EventEndDateTime.Date == item2.EventEndDateTime.Date &&
               item1.EventStartDateTime.Date == item2.EventStartDateTime.Date;
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

                sb.AppendLine("<tr>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td><a href=\"{item.Url}\">{item.Title}</a></td>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td>{item.EventStartDateTime:yyyy-MM-dd HH:mm} to<br/>{item.EventEndDateTime:yyyy-MM-dd HH:mm}<br/>({item.EnrollmentDeadline:yyyy-MM-dd})</td>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td>{item.Location?.Replace("\n", "<br/>")}</td>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td>{item.Price?.Replace("\n", "<br/>")}</td>");
                sb.AppendLine("</tr>");
                sb.AppendLine("<tr>");
                sb.AppendLine(CultureInfo.InvariantCulture, $"<td colspan=\"5\" class=\"description\">{description}</td>");
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