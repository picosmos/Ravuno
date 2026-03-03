using Ravuno.DataStorage.Models;

namespace Ravuno.WebAPI.Services.Contracts;

public interface IQueryService
{
    Task<List<Query>> GetAllByUserAsync(int userId);
    Task<Query?> GetByIdAsync(long id, int userId);
    Task<Query> CreateAsync(string title, string query, int userId);
    Task UpdateAsync(long id, string title, string query, int userId);
    Task DeleteAsync(long id, int userId);
    Task<bool> TitleExistsForUserAsync(string title, int userId, long? excludeId = null);
    bool ValidateSelectOnlyQuery(string query, out string? errorMessage);
}
