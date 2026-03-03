using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

public class AuthController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, ILogger<AuthController> logger)
    {
        this._userService = userService;
        this._logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (this.User.Identity?.IsAuthenticated == true)
        {
            return this.RedirectToAction("Index", "Home");
        }

        this.ViewBag.ReturnUrl = returnUrl;
        return this.View();
    }

    [AllowAnonymous]
    [HttpPost("/login")]
    public async Task<IActionResult> Login(
        string username,
        string password,
        string? returnUrl = null
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                this.ViewBag.Error = "Username and password are required";
                this.ViewBag.ReturnUrl = returnUrl;
                return this.View();
            }

            var user = await this._userService.GetByUsernameAsync(username);
            if (user == null || !await this._userService.ValidatePasswordAsync(user, password))
            {
                this._logger.LogWarning("Failed login attempt for username: {Username}", username);
                this.ViewBag.Error = "Invalid username or password";
                this.ViewBag.ReturnUrl = returnUrl;
                return this.View();
            }

            var claims = new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                ),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role.Name),
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
            };

            await this.HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            this._logger.LogInformation("User {Username} logged in successfully", username);

            if (!string.IsNullOrEmpty(returnUrl) && this.Url.IsLocalUrl(returnUrl))
            {
                return this.Redirect(returnUrl);
            }

            return this.RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error during login");
            this.ViewBag.Error = "An error occurred during login";
            this.ViewBag.ReturnUrl = returnUrl;
            return this.View();
        }
    }

    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await this.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        this._logger.LogInformation("User logged out");
        return this.RedirectToAction("Login");
    }

    [AllowAnonymous]
    [HttpGet("/access-denied")]
    public IActionResult AccessDenied()
    {
        return this.View();
    }
}
