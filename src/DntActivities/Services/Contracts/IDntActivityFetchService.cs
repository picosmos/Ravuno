using DataStorage.Models;

namespace DntActivities.Services.Contracts;

public interface IDntActivityFetchService
{
    Task<List<Item>> FetchItemsAsync();
}