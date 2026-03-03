using Ravuno.DataStorage.Models;

namespace Ravuno.DataStorage.Repositories.Contracts;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<List<User>> GetAllAsync();
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string username);
    Task<Role?> GetRoleByNameAsync(string roleName);
    Task<List<Role>> GetAllRolesAsync();
}
