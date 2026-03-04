using Ravuno.DataStorage.Models;
using Ravuno.WebAPI.Models;

namespace Ravuno.WebAPI.Services.Contracts;

public interface IQueryService
{
    // Query entity operations
    Task<List<Query>> GetAllByUserAsync(int userId);
    Task<Query?> GetByIdAsync(long id, int userId);
    Task<Query> CreateAsync(string title, string query, string email, int userId);
    Task UpdateAsync(long id, string title, string query, string email, int userId);
    Task ReassignQueryAsync(long id, int newUserId);
    Task DeleteAsync(long id, int userId);
    Task<bool> TitleExistsForUserAsync(string title, int userId, long? excludeId = null);
    bool ValidateSelectOnlyQuery(string query, out string? errorMessage);

    // UpdateConfiguration operations (merged from IUpdateConfigurationService)
    Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync();
    Task<List<UpdateConfiguration>> GetUpdateConfigurationsByUserAsync(int userId);
    Task<UpdateConfiguration?> GetUpdateConfigurationByIdAsync(long id);
    Task<UpdateConfiguration?> GetUpdateConfigurationByPublicIdAsync(string publicId);

    // SQL execution operations
    Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken = default
    );
}
