using Ravuno.DataStorage.Models;
using Ravuno.WebAPI.Models;

namespace Ravuno.WebAPI.Services.Contracts;

public interface IUpdateConfigurationService
{
    Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync();
    Task<List<UpdateConfiguration>> GetUpdateConfigurationsByUserAsync(int userId);
    Task<UpdateConfiguration?> GetUpdateConfigurationByIdAsync(long id);
    Task<UpdateConfiguration?> GetUpdateConfigurationByPublicIdAsync(string publicId);
    Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken = default
    );
}
