using Ravuno.DataStorage.Models;

namespace Ravuno.Fetcher.Contracts;

public interface IFetcherService
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<Item>> FetchAsync(IReadOnlyCollection<Item> alreadyFetched, bool detailed, CancellationToken cancellationToken);
}