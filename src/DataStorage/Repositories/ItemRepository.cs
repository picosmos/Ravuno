using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage.Models;
using Ravuno.DataStorage.Repositories.Contracts;

namespace Ravuno.DataStorage.Repositories;

public class ItemRepository(DataStorageContext dbContext) : IItemRepository
{
    private readonly DataStorageContext _dbContext = dbContext;

    public async Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlQuery);
        return await this
            ._dbContext.Database.SqlQueryRaw<Item>(sqlQuery)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
