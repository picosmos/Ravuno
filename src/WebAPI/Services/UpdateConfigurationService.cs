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
        var sqlScripts = await this
            ._dbContext.SqlScripts.Include(s => s.EmailReceivers)
            .AsNoTracking()
            .ToListAsync();

        return sqlScripts
            .Select(s => new UpdateConfiguration
            {
                Id = s.Id,
                QueryTitle = s.Title,
                SqlQuery = s.Query,
                EmailReceiverAddresses = s.EmailReceivers.Select(e => e.EmailAddress).ToList(),
            })
            .ToList();
    }

    public async Task<UpdateConfiguration?> GetUpdateConfigurationByTitleAsync(string queryTitle)
    {
        ArgumentNullException.ThrowIfNull(queryTitle);

        var sqlScript = await this
            ._dbContext.SqlScripts.Include(s => s.EmailReceivers)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => EF.Functions.Like(s.Title, queryTitle));

        if (sqlScript == null)
        {
            return null;
        }

        return new UpdateConfiguration
        {
            Id = sqlScript.Id,
            QueryTitle = sqlScript.Title,
            SqlQuery = sqlScript.Query,
            EmailReceiverAddresses = sqlScript.EmailReceivers.Select(e => e.EmailAddress).ToList(),
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
