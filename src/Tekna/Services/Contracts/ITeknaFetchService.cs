using DataStorage.Models;

namespace Tekna.Services.Contracts;

public interface ITeknaFetchService
{
    Task<List<Item>> FetchItemsAsync();
}