using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ravuno.DataStorage.Models;
using Ravuno.Fetcher.DntActivities.Services.Contracts;
using Ravuno.Fetcher.DntActivities.Settings;

namespace Ravuno.Fetcher.DntActivities.Services;

public class DntActivityFetchService : IDntActivityFetchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DntActivityFetchService> _logger;
    private readonly DntActivitiesSettings _settings;

    public DntActivityFetchService(HttpClient httpClient, IOptions<DntActivitiesSettings> settings, ILogger<DntActivityFetchService> logger)
    {
        this._httpClient = httpClient;
        this._logger = logger;
        this._settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool IsEnabled => this._settings.IsEnabled;

    public async Task<IReadOnlyList<Item>> FetchAsync(IReadOnlyCollection<Item> alreadyFetched, bool detailed, CancellationToken cancellationToken)
    {
        var allItems = new List<Item>();
        var pageNumber = 1;
        var hasMorePages = true;
        var failCount = 0;

        while (hasMorePages)
        {
            try
            {
                this._logger.LogInformation("Fetching DNT activities page {PageNumber}", pageNumber);

                var url = string.Format(CultureInfo.InvariantCulture, this._settings.ActivitiesApiUrl, pageNumber);
                var response = await this._httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(content);

                // Get page count and check if more pages
                if (jsonDoc.RootElement.TryGetProperty("pageCount", out var pageCountElement))
                {
                    var totalPages = pageCountElement.GetInt32();
                    this._logger.LogInformation("Processing page {CurrentPage} of {TotalPages}", pageNumber, totalPages);
                    hasMorePages = pageNumber < totalPages;
                }
                else
                {
                    hasMorePages = false;
                }

                // Get page hits (activities)
                if (jsonDoc.RootElement.TryGetProperty("pageHits", out var pageHitsElement) &&
                    pageHitsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var activityItem in pageHitsElement.EnumerateArray())
                    {
                        // Extract ID to fetch detailed data
                        var eventId = GetStringProperty(activityItem, "id");
                        if (string.IsNullOrEmpty(eventId))
                        {
                            this._logger.LogWarning("Activity item has no ID, skipping");
                            continue;
                        }
                        if (!detailed && alreadyFetched.Any(i => i.SourceId == eventId))
                        {
                            this._logger.LogInformation("Activity with ID {EventId} already exists, skipping detail fetch", eventId);
                            continue;
                        }

                        var detailedItem = await this.FetchEventDetailsAsync(eventId);
                        if (detailedItem != null && allItems.TrueForAll(x => x.SourceId != detailedItem.SourceId))
                        {
                            allItems.Add(detailedItem);

                            await Task.Delay(50, cancellationToken); // Add a small delay between detail requests
                        }
                    }

                    this._logger.LogInformation("Retrieved {Count} items from page {PageNumber}", pageHitsElement.GetArrayLength(), pageNumber);
                }

                pageNumber++;
                failCount = 0;

                if (hasMorePages)
                {
                    await Task.Delay(100, cancellationToken); // Add a small delay to be respectful to the API
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error fetching page {PageNumber} from DNT API", pageNumber);
                failCount++;
                if (failCount > 5)
                {
                    this._logger.LogError(ex, "Retried for 5 times, terminating dnt fetch");
                    return allItems;
                }
                throw;
            }
        }

        this._logger.LogInformation("Total items retrieved from DNT: {Count}", allItems.Count);
        return allItems;
    }

    private async Task<Item?> FetchEventDetailsAsync(string eventId)
    {
        string? content = null;
        try
        {
            this._logger.LogInformation("Fetching event details for ID {EventId}", eventId);

            var url = string.Format(CultureInfo.InvariantCulture, this._settings.EventDetailApiUrl, eventId);
            var response = await this._httpClient.GetAsync(url);

            content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // We do not validate the existence of the API here, we assume a bad ID was supplied. 
                // This happened before, either by just returning a 400 without content or a 400 with a status-json.
                this._logger.LogInformation("Fetching data for event with ID {EventId} returned status code {StatusCode}. Content: {Content}", eventId, response.StatusCode, content?[..Math.Min(500, content.Length)]);
                return null;
            }

            if (string.IsNullOrEmpty(content))
            {
                this._logger.LogInformation("Fetching data for event with ID {EventId} returned empty content.", eventId);
                return null;
            }

            var jsonDocument = JsonDocument.Parse(content);
            var root = jsonDocument.RootElement;

            var item = new Item
            {
                Source = ItemSource.DntActivities,
                RawData = content,
                SourceId = eventId,
                RetrievedAt = DateTime.UtcNow,
                Title = GetStringProperty(root, "name") ?? "No title",
                Description = GetStringProperty(root, "description") ?? "",
                Organizer = GetStringProperty(root, "organizerName") ?? "",
                Location = ExtractFullLocation(root),
                Url = $"https://aktiviteter.dnt.no/register/{eventId}",
                Price = ExtractPricesFromDetail(root),
                EventStartDateTime = GetDateTimePropertyFromRoot(root, "startDate") ?? DateTime.MinValue,
                EventEndDateTime = GetDateTimePropertyFromRoot(root, "endDate") ?? DateTime.MinValue,
                EnrollmentDeadline = GetDateTimePropertyFromRoot(root, "registrationEndDate")
                                   ?? GetDateTimePropertyFromRoot(root, "startDate")
                                   ?? DateTime.MinValue,
                Tags = ExtractTags(root)
            };

            return item;
        }
        catch (HttpRequestException httpEx)
        {
            this._logger.LogError(httpEx, "HTTP error fetching event details for ID {EventId}. Response content: {Content}", eventId, content);
            return null;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error fetching event details for ID {EventId}", eventId);
            return null;
        }
    }

    private static string ExtractFullLocation(JsonElement root)
    {
        var parts = new List<string>();

        if (root.TryGetProperty("location", out var locationElement))
        {
            var venue = GetStringProperty(locationElement, "venue");
            if (!string.IsNullOrEmpty(venue))
            {
                parts.Add(venue);
            }

            var address1 = GetStringProperty(locationElement, "address1");
            if (!string.IsNullOrEmpty(address1))
            {
                parts.Add(address1);
            }

            var address2 = GetStringProperty(locationElement, "address2");
            if (!string.IsNullOrEmpty(address2))
            {
                parts.Add(address2);
            }

            var postalCode = GetStringProperty(locationElement, "postalCode");
            var city = GetStringProperty(locationElement, "city");
            if (!string.IsNullOrEmpty(postalCode) && !string.IsNullOrEmpty(city))
            {
                parts.Add($"{postalCode} {city}");
            }
            else if (!string.IsNullOrEmpty(city))
            {
                parts.Add(city);
            }

            var country = GetStringProperty(locationElement, "country");
            if (!string.IsNullOrEmpty(country))
            {
                parts.Add(country);
            }
        }

        var municipalities = new List<string>();
        if (root.TryGetProperty("municipalities", out var meetingPointElement))
        {
            foreach (var municipality in meetingPointElement.EnumerateArray())
            {
                var name = GetStringProperty(municipality, "name");
                if (!string.IsNullOrEmpty(name) && !parts.Contains(name))
                {
                    municipalities.Add(name);
                }
            }
        }

        var municipalitiesStr = string.Join(", ", municipalities);
        if (!string.IsNullOrEmpty(municipalitiesStr))
        {
            parts.Add("");
            parts.Add("Municipalities: " + municipalitiesStr);
        }

        return parts.Count > 0 ? string.Join("\n", parts) : "Location not specified";
    }

    private static string ExtractPricesFromDetail(JsonElement root)
    {
        var currency = GetStringProperty(root, "currency") ?? "NOK";

        if (root.TryGetProperty("prices", out var pricesElement) &&
            pricesElement.ValueKind == JsonValueKind.Array &&
            pricesElement.GetArrayLength() > 0)
        {
            var prices = new List<string>();
            foreach (var price in pricesElement.EnumerateArray())
            {
                var name = GetStringProperty(price, "name") ?? "";
                var value = GetStringProperty(price, "value") ?? "0";
                var vat = GetStringProperty(price, "vat") ?? "0";

                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    sb.Append(name);
                }

                if (!string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(@"^0\.(0+)$", value))
                {
                    sb.Append(' ').Append(value).Append(' ').Append(currency);
                }

                if (!string.IsNullOrWhiteSpace(vat) && !Regex.IsMatch(@"^0\.(0+)$", vat))
                {
                    sb.Append(" (mva: ").Append(vat).Append(' ').Append(currency).Append(')');
                }

                prices.Add(sb.ToString());
            }

            if (prices.Count > 0)
            {
                return string.Join("\n", prices);
            }
        }

        return "Price not available";
    }

    private static string[] ExtractTags(JsonElement root)
    {
        var tagsList = new List<string>();

        // Get mainTags
        if (root.TryGetProperty("mainTags", out var mainTagsElement) &&
            mainTagsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var mainTag in mainTagsElement.EnumerateArray())
            {
                var isActive = mainTag.TryGetProperty("isActive", out var isActiveElement)
                    ? isActiveElement.GetInt32()
                    : 0;

                if (isActive != 1)
                {
                    continue;
                }

                var mainTagName = GetStringProperty(mainTag, "name");
                if (string.IsNullOrEmpty(mainTagName))
                {
                    continue;
                }

                var mainTagId = mainTag.TryGetProperty("id", out var mainTagIdElement)
                    ? mainTagIdElement.GetInt32()
                    : 0;

                // Find related sub-tags
                var subTags = new List<string>();
                if (root.TryGetProperty("tags", out var tagsElement) &&
                    tagsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsElement.EnumerateArray())
                    {
                        var tagIsActive = tag.TryGetProperty("isActive", out var tagIsActiveElement)
                            ? tagIsActiveElement.GetInt32()
                            : 0;

                        if (tagIsActive != 1)
                        {
                            continue;
                        }

                        var parentId = tag.TryGetProperty("parentId", out var parentIdElement)
                            ? parentIdElement.GetInt32()
                            : 0;

                        if (parentId == mainTagId)
                        {
                            var subTagName = GetStringProperty(tag, "name");
                            if (!string.IsNullOrEmpty(subTagName))
                            {
                                subTags.Add(subTagName);
                            }
                        }
                    }
                }

                // Format: mainTag: |subTag1|subTag2|subTag3|
                if (subTags.Count > 0)
                {
                    tagsList.Add($"{mainTagName}: |{string.Join("|", subTags)}|");
                }
                else
                {
                    tagsList.Add($"{mainTagName}:");
                }
            }
        }

        return [.. tagsList];
    }

    private static DateTime? GetDateTimePropertyFromRoot(JsonElement element, string propertyName)
    {
        var stringValue = GetStringProperty(element, propertyName);
        if (string.IsNullOrEmpty(stringValue))
        {
            return null;
        }

        // DNT specifies the DateTimes including the timezone offset
        // e.g., "2024-08-15T10:00:00+02:00"
        // since we are storing the local time as DateTimeKind.Local, we ignore the timezone offset here.
        return DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime)
            ? dateTime
            : null;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            if (property.ValueKind == JsonValueKind.Number)
            {
                return property.GetDouble().ToString(CultureInfo.InvariantCulture);
            }

            if (property.ValueKind != JsonValueKind.Null)
            {
                return property.ToString();
            }
        }
        return null;
    }
}