using Ravuno.DataStorage.Models;
using Ravuno.WebAPI.Models;

namespace Ravuno.WebAPI.Services.Contracts;

public interface IUpdateConfigurationService
{
    Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync();
    Task<UpdateConfiguration?> GetUpdateConfigurationByTitleAsync(string queryTitle);
    Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken = default
    );
}
