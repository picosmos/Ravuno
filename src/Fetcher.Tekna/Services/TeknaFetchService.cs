using System.Globalization;
using System.Text.Json;
using Ravuno.DataStorage.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ravuno.Fetcher.Tekna.Services.Contracts;
using Ravuno.Fetcher.Tekna.Settings;

namespace Ravuno.Fetcher.Tekna.Services;

//cspell:ignore searchtargetgroup, fieldsofstudy, pricegroup, regiondigital

public class TeknaFetchService : ITeknaFetchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TeknaFetchService>? _logger;
    private readonly TeknaSettings _settings;

    public TeknaFetchService(HttpClient httpClient, IOptions<TeknaSettings> settings, ILogger<TeknaFetchService>? logger = null)
    {
        this._httpClient = httpClient;
        this._logger = logger;
        this._settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<List<Item>> FetchItemsAsync()
    {
        var allItems = new List<Item>();
        var pageNumber = 1;
        var hasMorePages = true;

        while (hasMorePages)
        {
            try
            {
                this._logger?.LogInformation("Fetching Tekna courses page {PageNumber}", pageNumber);

                var url = string.Format(CultureInfo.InvariantCulture, this._settings.CoursesApiUrl, pageNumber);
                var response = await this._httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                // Check if Courses section exists
                if (!jsonDoc.RootElement.TryGetProperty("Courses", out var coursesElement))
                {
                    this._logger?.LogWarning("No Courses property found in response");
                    break;
                }

                // Get paging information
                if (coursesElement.TryGetProperty("Paging", out var pagingElement))
                {
                    var currentPage = pagingElement.GetProperty("PageNumber").GetInt32();
                    var totalPages = pagingElement.GetProperty("NumPages").GetInt32();

                    this._logger?.LogInformation("Processing page {CurrentPage} of {TotalPages}", currentPage, totalPages);

                    hasMorePages = currentPage < totalPages;
                }
                else
                {
                    hasMorePages = false;
                }

                // Get refiner mappings
                var refinerMappings = this.BuildRefinerMappings(coursesElement);

                // Get items
                if (coursesElement.TryGetProperty("Items", out var itemsElement))
                {
                    var items = itemsElement.EnumerateArray();

                    foreach (var courseItem in items)
                    {
                        try
                        {
                            var organizerItem = courseItem.GetProperty("Organizer");
                            var organizer = organizerItem.ValueKind == JsonValueKind.Null ? null : this.GetStringProperty(organizerItem, "Name") ?? null;
                            var subOrganizerItem = courseItem.GetProperty("SubOrganizer");
                            var subOrganizer = subOrganizerItem.ValueKind == JsonValueKind.Null ? null : this.GetStringProperty(subOrganizerItem, "Name") ?? null;
                            var fullOrganizerName = string.Join("\n", new[] { subOrganizer, organizer }.Where(s => !string.IsNullOrEmpty(s)));

                            var item = new Item
                            {
                                Source = ItemSource.Tekna,
                                RawData = courseItem.GetRawText(),
                                SourceId = this.GetStringProperty(courseItem, "Id") ?? string.Empty,
                                RetrievedAt = DateTime.UtcNow,
                                Title = this.GetStringProperty(courseItem, "Title") ?? "No title",
                                Description = this.GetStringProperty(courseItem, "Ingress") + "\n" + this.GetStringProperty(courseItem, "Description"),
                                Location = string.Join("\n", new[] {
                                        this.GetStringProperty(courseItem, "VenueDetail"),
                                        this.GetStringProperty(courseItem, "VenueName"),
                                        this.GetStringProperty(courseItem, "VenueTown") ,
                                        this.GetStringProperty(courseItem, "Region"),
                                        this.GetStringProperty(courseItem, "District"),
                                        this.GetStringProperty(courseItem, "County")
                                }.Where(s => !string.IsNullOrEmpty(s))),
                                Organizer = fullOrganizerName,
                                Url = this.GetStringProperty(courseItem, "PublicUrl") ?? "",
                                Price = this.ExtractPrice(courseItem),
                                EventStartDateTime = this.GetDateTimeProperty(courseItem, "StartDate") ?? DateTime.MinValue,
                                EventEndDateTime = this.GetDateTimeProperty(courseItem, "EndDate") ?? DateTime.MinValue,
                                EnrollmentDeadline = this.GetDateTimeProperty(courseItem, "EnrollmentDeadline")
                                                   ?? this.GetDateTimeProperty(courseItem, "StartDate")
                                                   ?? DateTime.MinValue,
                                Tags = this.ExtractTags(courseItem, refinerMappings)
                            };

                            allItems.Add(item);
                        }
                        catch (Exception ex)
                        {
                            this._logger?.LogError(ex, "Error processing course item: {CourseItem}", courseItem.GetRawText());
                        }
                    }

                    this._logger?.LogInformation("Retrieved {Count} items from page {PageNumber}", itemsElement.GetArrayLength(), pageNumber);
                }

                pageNumber++;

                // Add a small delay to be respectful to the API
                if (hasMorePages)
                {
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                this._logger?.LogError(ex, "Error fetching page {PageNumber} from Tekna API", pageNumber);
                throw;
            }
        }

        this._logger?.LogInformation("Total items retrieved from Tekna: {Count}", allItems.Count);
        return allItems;
    }

    private string? GetStringProperty(JsonElement element, string propertyName)
    {
        try
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.String)
                {
                    return property.GetString() ?? string.Empty;
                }
                if (property.ValueKind != JsonValueKind.Null)
                {
                    return property.ToString() ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            this._logger?.LogError(ex, "Error getting string property {PropertyName}", propertyName);
            throw;
        }
        return string.Empty;
    }

    private DateTime? GetDateTimeProperty(JsonElement element, string propertyName)
    {
        var stringValue = this.GetStringProperty(element, propertyName);
        return DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, out var dateTime)
            ? dateTime
            : null;
    }

    private string ExtractPrice(JsonElement courseItem)
    {
        // Try to get price from PriceGroup first
        var priceGroup = this.GetStringProperty(courseItem, "PriceGroup");

        // Try to get prices from Prices array
        if (courseItem.TryGetProperty("Prices", out var pricesElement) &&
            pricesElement.ValueKind == JsonValueKind.Array &&
            pricesElement.GetArrayLength() > 0)
        {
            var prices = new List<string>();
            foreach (var price in pricesElement.EnumerateArray())
            {
                if (price.TryGetProperty("Amount", out var amount))
                {
                    var priceValue = amount.ToString();
                    var priceType = this.GetStringProperty(price, "Name") ?? "";
                    var hasMvaString = bool.TryParse(this.GetStringProperty(price, "HasMva") ?? "false", out var hasMva) && hasMva ? "med mva" : "uten mva";

                    prices.Add($"{priceType}: {priceValue} NOK ({hasMvaString})");
                }
            }

            if (prices.Count != 0)
            {
                return string.Join("\n", prices);
            }
        }

        return !string.IsNullOrEmpty(priceGroup) && priceGroup != "0" ? $"Price group: {priceGroup}" : "Price not available";
    }

    private Dictionary<string, Dictionary<string, string>> BuildRefinerMappings(JsonElement coursesElement)
    {
        var mappings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (!coursesElement.TryGetProperty("Refiners", out var refinersElement))
        {
            return mappings;
        }

        foreach (var refiner in refinersElement.EnumerateArray())
        {
            var refinerId = this.GetStringProperty(refiner, "Id")?.ToLowerInvariant();
            if (string.IsNullOrEmpty(refinerId))
            {
                continue;
            }

            var itemMap = new Dictionary<string, string>();
            if (refiner.TryGetProperty("Items", out var itemsElement))
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var id = this.GetStringProperty(item, "Id");
                    var label = this.GetStringProperty(item, "Label");
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(label))
                    {
                        itemMap[id] = label;
                    }
                }
            }

            mappings[refinerId] = itemMap;
        }

        return mappings;
    }

    private string[] ExtractTags(JsonElement courseItem, Dictionary<string, Dictionary<string, string>> refinerMappings)
    {
        var tags = new List<string>();

        // Extract regiondigital (Region/District/County hierarchy)
        var region = this.GetStringProperty(courseItem, "Region")?.Trim();
        var district = this.GetStringProperty(courseItem, "District")?.Trim();
        var county = this.GetStringProperty(courseItem, "County")?.Trim();

        var regionParts = new List<string>();
        if (!string.IsNullOrEmpty(region))
        {
            regionParts.Add(region);
        }
        if (!string.IsNullOrEmpty(district))
        {
            regionParts.Add(district);
        }
        if (!string.IsNullOrEmpty(county))
        {
            regionParts.Add(county);
        }

        if (regionParts.Count > 0)
        {
            tags.Add($"regiondigital={string.Join("/", regionParts)}");
        }

        // Extract searchtargetgroup
        if (courseItem.TryGetProperty("SearchTargetGroup", out var searchTargetGroupElement))
        {
            var searchTargetGroupValue = searchTargetGroupElement.ValueKind == JsonValueKind.Number
                ? searchTargetGroupElement.GetInt32().ToString(CultureInfo.InvariantCulture)
                : this.GetStringProperty(courseItem, "SearchTargetGroup");

            if (!string.IsNullOrEmpty(searchTargetGroupValue) &&
                refinerMappings.TryGetValue("searchtargetgroup", out var targetGroupMap) &&
                targetGroupMap.TryGetValue(searchTargetGroupValue, out var targetGroupLabel))
            {
                tags.Add($"searchtargetgroup={targetGroupLabel}");
            }
        }

        // Extract fieldsofstudy
        if (courseItem.TryGetProperty("FieldsOfStudy", out var fieldsOfStudyElement) &&
            fieldsOfStudyElement.ValueKind == JsonValueKind.Array &&
            refinerMappings.TryGetValue("fieldsofstudy", out var fieldsMap))
        {
            foreach (var fieldId in fieldsOfStudyElement.EnumerateArray())
            {
                var fieldIdString = fieldId.ValueKind == JsonValueKind.String
                    ? fieldId.GetString()
                    : fieldId.ToString();

                if (!string.IsNullOrEmpty(fieldIdString) && fieldsMap.TryGetValue(fieldIdString, out var fieldLabel))
                {
                    tags.Add($"fieldsofstudy={fieldLabel}");
                }
            }
        }

        // Extract pricegroup
        var priceGroup = this.GetStringProperty(courseItem, "PriceGroup");
        if (!string.IsNullOrEmpty(priceGroup) &&
            refinerMappings.TryGetValue("pricegroup", out var priceGroupMap) &&
            priceGroupMap.TryGetValue(priceGroup, out var priceGroupLabel))
        {
            tags.Add($"pricegroup={priceGroupLabel}");
        }

        // Extract language
        if (courseItem.TryGetProperty("Language", out var languageElement))
        {
            var languageValue = languageElement.ValueKind == JsonValueKind.Number
                ? languageElement.GetInt32().ToString(CultureInfo.InvariantCulture)
                : this.GetStringProperty(courseItem, "Language");

            if (!string.IsNullOrEmpty(languageValue) &&
                refinerMappings.TryGetValue("language", out var languageMap) &&
                languageMap.TryGetValue(languageValue, out var languageLabel))
            {
                tags.Add($"language={languageLabel}");
            }
        }

        return [.. tags];
    }
}