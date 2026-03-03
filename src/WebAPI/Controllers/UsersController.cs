using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ravuno.DataStorage.Constants;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        this._userService = userService;
        this._logger = logger;
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("/users")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var users = await this._userService.GetAllUsersAsync();
            return this.View(users);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading users list");
            return this.StatusCode(500, "Error loading users list");
        }
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("/users/create")]
    public async Task<IActionResult> Create()
    {
        try
        {
            var roles = await this._userService.GetAllRolesAsync();
            this.ViewBag.Roles = roles;
            return this.View();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading create user page");
            return this.StatusCode(500, "Error loading page");
        }
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("/users/create")]
    public async Task<IActionResult> Create(string username, string password, int roleId)
    {
        try
        {
            var roles = await this._userService.GetAllRolesAsync();
            this.ViewBag.Roles = roles;
            this.ViewBag.Username = username;
            this.ViewBag.RoleId = roleId;

            if (string.IsNullOrWhiteSpace(username))
            {
                this.ViewBag.Error = "Username is required";
                return this.View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                this.ViewBag.Error = "Password is required";
                return this.View();
            }

            if (await this._userService.UsernameExistsAsync(username))
            {
                this.ViewBag.Error = $"Username '{username}' already exists";
                return this.View();
            }

            if (!this._userService.ValidatePasswordStrength(password, out var passwordError))
            {
                this.ViewBag.Error = passwordError;
                return this.View();
            }

            await this._userService.CreateUserAsync(username, password, roleId);
            this._logger.LogInformation("User {Username} created successfully", username);
            return this.RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error creating user");
            this.ViewBag.Error = "Error creating user: " + ex.Message;
            var roles = await this._userService.GetAllRolesAsync();
            this.ViewBag.Roles = roles;
            this.ViewBag.Username = username;
            this.ViewBag.RoleId = roleId;
            return this.View();
        }
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("/users/delete/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var currentUserId = int.Parse(
                this.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0",
                System.Globalization.CultureInfo.InvariantCulture
            );

            if (id == currentUserId)
            {
                this._logger.LogWarning("User {UserId} attempted to delete their own account", id);
                return this.BadRequest("You cannot delete your own account");
            }

            await this._userService.DeleteUserAsync(id);
            this._logger.LogInformation("User {UserId} deleted successfully", id);
            return this.RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error deleting user {UserId}", id);
            return this.StatusCode(500, "Error deleting user");
        }
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("/users/edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var user = await this._userService.GetByIdAsync(id);
            if (user == null)
            {
                return this.NotFound("User not found");
            }

            var roles = await this._userService.GetAllRolesAsync();
            this.ViewBag.Roles = roles;
            return this.View(user);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading edit user page");
            return this.StatusCode(500, "Error loading page");
        }
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("/users/edit/{id}")]
    public async Task<IActionResult> Edit(int id, string username, int roleId)
    {
        try
        {
            var user = await this._userService.GetByIdAsync(id);
            if (user == null)
            {
                return this.NotFound("User not found");
            }

            var roles = await this._userService.GetAllRolesAsync();
            this.ViewBag.Roles = roles;

            if (string.IsNullOrWhiteSpace(username))
            {
                this.ViewBag.Error = "Username is required";
                return this.View(user);
            }

            await this._userService.UpdateUserAsync(id, username, roleId);
            this._logger.LogInformation("User {UserId} updated successfully", id);
            return this.RedirectToAction("Index");
        }
        catch (InvalidOperationException ex)
        {
            this._logger.LogWarning(ex, "Error updating user {UserId}", id);
            this.ViewBag.Error = ex.Message;
            var user = await this._userService.GetByIdAsync(id);
            var roles = await this._userService.GetAllRolesAsync();
            this.ViewBag.Roles = roles;
            return this.View(user);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error updating user {UserId}", id);
            this.ViewBag.Error = "Error updating user: " + ex.Message;
            var user = await this._userService.GetByIdAsync(id);
            var roles = await this._userService.GetAllRolesAsync();
            this.ViewBag.Roles = roles;
            return this.View(user);
        }
    }

    [HttpGet("/users/change-password")]
    public IActionResult ChangePassword()
    {
        return this.View();
    }

    [HttpPost("/users/change-password")]
    public async Task<IActionResult> ChangePassword(
        string currentPassword,
        string newPassword,
        string confirmPassword
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                this.ViewBag.Error = "Current password is required";
                return this.View();
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                this.ViewBag.Error = "New password is required";
                return this.View();
            }

            if (newPassword != confirmPassword)
            {
                this.ViewBag.Error = "New passwords do not match";
                return this.View();
            }

            var userId = int.Parse(
                this.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0",
                System.Globalization.CultureInfo.InvariantCulture
            );
            var user = await this._userService.GetByIdAsync(userId);

            if (user == null)
            {
                return this.Unauthorized();
            }

            if (!await this._userService.ValidatePasswordAsync(user, currentPassword))
            {
                this.ViewBag.Error = "Current password is incorrect";
                return this.View();
            }

            if (!this._userService.ValidatePasswordStrength(newPassword, out var passwordError))
            {
                this.ViewBag.Error = passwordError;
                return this.View();
            }

            await this._userService.ChangePasswordAsync(userId, newPassword);
            this._logger.LogInformation("User {UserId} changed password successfully", userId);
            this.ViewBag.Success = "Password changed successfully";
            return this.View();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error changing password");
            this.ViewBag.Error = "Error changing password: " + ex.Message;
            return this.View();
        }
    }
}
