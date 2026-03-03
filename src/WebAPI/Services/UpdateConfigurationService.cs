using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;
using Ravuno.DataStorage.Models;
using Ravuno.WebAPI.Models;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Services;

public class UpdateConfigurationService(DataStorageContext dbContext) : IUpdateConfigurationService
{
    private readonly DataStorageContext _dbContext = dbContext;

    public async Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync()
    {
        var queries = await this
            ._dbContext.Queries.Include(s => s.EmailReceivers)
            .AsNoTracking()
            .ToListAsync();

        return
        [
            .. queries.Select(s => new UpdateConfiguration
            {
                Id = s.Id,
                QueryTitle = s.Title,
                SqlQuery = s.SqlQuery,
                PublicId = s.PublicId,
                EmailReceiverAddresses = [.. s.EmailReceivers.Select(e => e.EmailAddress)],
            }),
        ];
    }

    public async Task<List<UpdateConfiguration>> GetUpdateConfigurationsByUserAsync(int userId)
    {
        var queries = await this
            ._dbContext.Queries.Include(s => s.EmailReceivers)
            .Where(s => s.UserId == userId)
            .AsNoTracking()
            .ToListAsync();

        return
        [
            .. queries.Select(s => new UpdateConfiguration
            {
                Id = s.Id,
                QueryTitle = s.Title,
                SqlQuery = s.SqlQuery,
                PublicId = s.PublicId,
                EmailReceiverAddresses = [.. s.EmailReceivers.Select(e => e.EmailAddress)],
            }),
        ];
    }

    public async Task<UpdateConfiguration?> GetUpdateConfigurationByIdAsync(long id)
    {
        var query = await this
            ._dbContext.Queries.Include(s => s.EmailReceivers)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (query == null)
        {
            return null;
        }

        return new UpdateConfiguration
        {
            Id = query.Id,
            QueryTitle = query.Title,
            SqlQuery = query.SqlQuery,
            PublicId = query.PublicId,
            EmailReceiverAddresses = [.. query.EmailReceivers.Select(e => e.EmailAddress)],
        };
    }

    public async Task<UpdateConfiguration?> GetUpdateConfigurationByPublicIdAsync(string publicId)
    {
        var query = await this
            ._dbContext.Queries.Include(s => s.EmailReceivers)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PublicId == publicId);

        if (query == null)
        {
            return null;
        }

        return new UpdateConfiguration
        {
            Id = query.Id,
            QueryTitle = query.Title,
            SqlQuery = query.SqlQuery,
            PublicId = query.PublicId,
            EmailReceiverAddresses = [.. query.EmailReceivers.Select(e => e.EmailAddress)],
        };
    }

    public async Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken = default
    )
    {
        return await this
            ._dbContext.Database.SqlQueryRaw<Item>(sqlQuery)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
