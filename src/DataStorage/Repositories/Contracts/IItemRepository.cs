using Ravuno.DataStorage.Models;

namespace Ravuno.DataStorage.Repositories.Contracts;

public interface IItemRepository
{
    Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken = default
    );
}
