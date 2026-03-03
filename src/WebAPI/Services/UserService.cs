using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Ravuno.DataStorage.Models;
using Ravuno.DataStorage.Repositories.Contracts;
using Ravuno.WebAPI.Services.Contracts;
using Ravuno.WebAPI.Settings;

namespace Ravuno.WebAPI.Services;

public partial class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordSettings _passwordSettings;

    public UserService(IUserRepository userRepository, IOptions<PasswordSettings> passwordSettings)
    {
        ArgumentNullException.ThrowIfNull(passwordSettings);
        this._userRepository = userRepository;
        this._passwordSettings = passwordSettings.Value;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await this._userRepository.GetByIdAsync(id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await this._userRepository.GetByUsernameAsync(username);
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await this._userRepository.GetAllAsync();
    }

    public async Task<User> CreateUserAsync(string username, string password, int roleId)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        if (await this._userRepository.ExistsAsync(username))
        {
            throw new InvalidOperationException($"User with username '{username}' already exists.");
        }

        if (!this.ValidatePasswordStrength(password, out var errorMessage))
        {
            throw new InvalidOperationException(
                errorMessage ?? "Password does not meet strength requirements."
            );
        }

        var salt = this.GenerateSalt();
        var passwordHash = this.HashPassword(password, salt);

        var user = new User
        {
            Username = username,
            PasswordHash = passwordHash,
            Salt = salt,
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow,
        };

        return await this._userRepository.CreateAsync(user);
    }

    public async Task DeleteUserAsync(int id)
    {
        await this._userRepository.DeleteAsync(id);
    }

    public Task<bool> ValidatePasswordAsync(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var hashedPassword = this.HashPassword(password, user.Salt);
        return Task.FromResult(hashedPassword == user.PasswordHash);
    }

    public async Task ChangePasswordAsync(int userId, string newPassword)
    {
        ArgumentNullException.ThrowIfNull(newPassword);

        if (!this.ValidatePasswordStrength(newPassword, out var errorMessage))
        {
            throw new InvalidOperationException(
                errorMessage ?? "Password does not meet strength requirements."
            );
        }

        var user =
            await this._userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var salt = this.GenerateSalt();
        user.PasswordHash = this.HashPassword(newPassword, salt);
        user.Salt = salt;

        await this._userRepository.UpdateAsync(user);
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        return await this._userRepository.ExistsAsync(username);
    }

    public async Task<List<Role>> GetAllRolesAsync()
    {
        return await this._userRepository.GetAllRolesAsync();
    }

    public async Task<Role?> GetRoleByNameAsync(string roleName)
    {
        return await this._userRepository.GetRoleByNameAsync(roleName);
    }

    public string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combined = new byte[saltBytes.Length + passwordBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, combined, saltBytes.Length, passwordBytes.Length);

        var hashBytes = SHA256.HashData(combined);
        return Convert.ToBase64String(hashBytes);
    }

    public string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(saltBytes);
    }

    public bool ValidatePasswordStrength(string password, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Password cannot be empty.";
            return false;
        }

        if (password.Length < this._passwordSettings.MinLength)
        {
            errorMessage =
                $"Password must be at least {this._passwordSettings.MinLength} characters long.";
            return false;
        }

        if (this._passwordSettings.RequireUppercase && !UppercaseRegex().IsMatch(password))
        {
            errorMessage = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (this._passwordSettings.RequireLowercase && !LowercaseRegex().IsMatch(password))
        {
            errorMessage = "Password must contain at least one lowercase letter.";
            return false;
        }

        if (this._passwordSettings.RequireDigit && !DigitRegex().IsMatch(password))
        {
            errorMessage = "Password must contain at least one digit.";
            return false;
        }

        if (this._passwordSettings.RequireSpecialChar && !SpecialCharRegex().IsMatch(password))
        {
            errorMessage = "Password must contain at least one special character.";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"[A-Z]")]
    private static partial Regex UppercaseRegex();

    [GeneratedRegex(@"[a-z]")]
    private static partial Regex LowercaseRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex DigitRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex SpecialCharRegex();
}
