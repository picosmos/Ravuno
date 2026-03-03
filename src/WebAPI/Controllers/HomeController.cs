using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IUpdateConfigurationService _updateConfigService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IUpdateConfigurationService updateConfigService,
        ILogger<HomeController> logger
    )
    {
        this._updateConfigService = updateConfigService;
        this._logger = logger;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = int.Parse(
                this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0",
                System.Globalization.CultureInfo.InvariantCulture
            );

            var configurations = await this._updateConfigService.GetUpdateConfigurationsByUserAsync(
                userId
            );
            this.ViewBag.QueryTitles = configurations.Select(c => c.QueryTitle).ToList();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading update configurations for home page");
            this.ViewBag.QueryTitles = new List<string>();
        }

        return this.View();
    }
}
