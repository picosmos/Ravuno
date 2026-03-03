using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ravuno.WebAPI.Models;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IQueryService _queryService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IQueryService queryService, ILogger<HomeController> logger)
    {
        this._queryService = queryService;
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

            var configurations = await this._queryService.GetUpdateConfigurationsByUserAsync(
                userId
            );
            this.ViewBag.Queries = configurations;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading update configurations for home page");
            this.ViewBag.Queries = new List<UpdateConfiguration>();
        }

        return this.View();
    }
}
