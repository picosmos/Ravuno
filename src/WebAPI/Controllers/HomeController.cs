using Microsoft.AspNetCore.Mvc;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

public class HomeController : Controller
{
    private readonly IUpdateConfigurationService _updateConfigService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IUpdateConfigurationService updateConfigService,
        ILogger<HomeController> logger)
    {
        this._updateConfigService = updateConfigService;
        this._logger = logger;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var configurations = await this._updateConfigService.GetUpdateConfigurationsAsync();
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
