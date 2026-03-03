using Ravuno.DataStorage.Models;

namespace Ravuno.WebAPI.Services.Contracts;

public interface IUserService
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByUsernameAsync(string username);
    Task<List<User>> GetAllUsersAsync();
    Task<User> CreateUserAsync(string username, string password, int roleId);
    Task UpdateUserAsync(int id, string username, int roleId);
    Task DeleteUserAsync(int id);
    Task<bool> ValidatePasswordAsync(User user, string password);
    Task ChangePasswordAsync(int userId, string newPassword);
    Task<bool> UsernameExistsAsync(string username);
    Task<List<Role>> GetAllRolesAsync();
    Task<Role?> GetRoleByNameAsync(string roleName);
    string HashPassword(string password, string salt);
    string GenerateSalt();
    bool ValidatePasswordStrength(string password, out string? errorMessage);
}
