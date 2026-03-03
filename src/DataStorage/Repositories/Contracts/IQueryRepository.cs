using Ravuno.DataStorage.Models;

namespace Ravuno.DataStorage.Repositories.Contracts;

public interface IQueryRepository
{
    Task<List<Query>> GetAllAsync();
    Task<List<Query>> GetAllByUserAsync(int userId);
    Task<Query?> GetByIdAsync(long id);
    Task<Query?> GetByIdAndUserAsync(long id, int userId);
    Task<Query?> GetByPublicIdAsync(string publicId);
    Task<Query> CreateAsync(Query query);
    Task UpdateAsync(Query query);
    Task DeleteAsync(Query query);
    Task<bool> TitleExistsForUserAsync(string title, int userId, long? excludeId = null);
}
