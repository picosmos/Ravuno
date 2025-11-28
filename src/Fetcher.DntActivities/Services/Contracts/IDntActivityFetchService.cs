using Ravuno.DataStorage.Models;

namespace Ravuno.Fetcher.DntActivities.Services.Contracts;

public interface IDntActivityFetchService
{
    Task<List<Item>> FetchItemsAsync(IReadOnlyCollection<Item> existingItems, bool fetchDetailsForExisting = false);
}