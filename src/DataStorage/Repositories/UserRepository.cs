using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage.Models;
using Ravuno.DataStorage.Repositories.Contracts;

namespace Ravuno.DataStorage.Repositories;

public class UserRepository(DataStorageContext dbContext) : IUserRepository
{
    private readonly DataStorageContext _dbContext = dbContext;

    public async Task<User?> GetByIdAsync(int id)
    {
        return await this
            ._dbContext.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        return await this
            ._dbContext.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await this
            ._dbContext.Users.Include(u => u.Role)
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        this._dbContext.Users.Add(user);
        await this._dbContext.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        this._dbContext.Users.Update(user);
        await this._dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var user = await this._dbContext.Users.FindAsync(id);
        if (user != null)
        {
            this._dbContext.Users.Remove(user);
            await this._dbContext.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        return await this._dbContext.Users.AnyAsync(u => u.Username == username);
    }

    public async Task<Role?> GetRoleByNameAsync(string roleName)
    {
        ArgumentNullException.ThrowIfNull(roleName);
        return await this._dbContext.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await this._dbContext.Roles.OrderBy(r => r.Name).ToListAsync();
    }
}
