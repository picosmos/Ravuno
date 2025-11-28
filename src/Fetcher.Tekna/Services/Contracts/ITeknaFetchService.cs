using Ravuno.DataStorage.Models;

namespace Ravuno.Fetcher.Tekna.Services.Contracts;

public interface ITeknaFetchService
{
    Task<List<Item>> FetchItemsAsync();
}