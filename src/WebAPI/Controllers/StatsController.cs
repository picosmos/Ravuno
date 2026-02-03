using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[Route("stats")]
public class StatsController : Controller
{
    private readonly DataStorageContext _context;
    private readonly IUpdateConfigurationService _updateConfigService;
    private readonly ILogger<StatsController> _logger;

    public StatsController(
        DataStorageContext context,
        IUpdateConfigurationService updateConfigService,
        ILogger<StatsController> logger)
    {
        this._context = context;
        this._updateConfigService = updateConfigService;
        this._logger = logger;
    }

    [HttpGet("fetchhistory")]
    public async Task<IActionResult> FetchHistory(int page = 1, int pageSize = 100)
    {
        if (page < 1)
        {
            page = 1;
        }

        var totalCount = await this._context.FetchHistories.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var histories = await this._context.FetchHistories
            .OrderByDescending(h => h.ExecutionStartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        this.ViewBag.CurrentPage = page;
        this.ViewBag.TotalPages = totalPages;
        this.ViewBag.PageSize = pageSize;
        this.ViewBag.TotalCount = totalCount;

        return this.View(histories);
    }

    [HttpGet("sendupdatehistory")]
    public async Task<IActionResult> SendUpdateHistory(int page = 1, int pageSize = 100)
    {
        if (page < 1)
        {
            page = 1;
        }

        var totalCount = await this._context.SendUpdateHistories.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var histories = await this._context.SendUpdateHistories
            .OrderByDescending(h => h.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        this.ViewBag.CurrentPage = page;
        this.ViewBag.TotalPages = totalPages;
        this.ViewBag.PageSize = pageSize;
        this.ViewBag.TotalCount = totalCount;

        return this.View(histories);
    }

    [HttpGet("items")]
    public async Task<IActionResult> Items(int page = 1, int pageSize = 100)
    {
        if (page < 1)
        {
            page = 1;
        }

        var totalCount = await this._context.Items.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await this._context.Items
            .OrderByDescending(i => i.RetrievedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        this.ViewBag.CurrentPage = page;
        this.ViewBag.TotalPages = totalPages;
        this.ViewBag.PageSize = pageSize;
        this.ViewBag.TotalCount = totalCount;

        return this.View(items);
    }

    [HttpGet("viewsql/{queryTitle}")]
    public async Task<IActionResult> ViewSql(string queryTitle, int page = 1, int pageSize = 100)
    {
        if (page < 1)
        {
            page = 1;
        }

        try
        {
            var config = await this._updateConfigService.GetUpdateConfigurationByTitleAsync(queryTitle);

            if (config == null)
            {
                return this.NotFound();
            }

            // Execute SQL query
            var allResults = await this._updateConfigService.ExecuteSqlQueryAsync(config.SqlQuery, this.HttpContext.RequestAborted);

            var totalCount = allResults.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = allResults
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            this.ViewBag.QueryTitle = config.QueryTitle;
            this.ViewBag.SqlQuery = config.SqlQuery;
            this.ViewBag.EmailReceiver = config.EmailReceiverAddress;
            this.ViewBag.CurrentPage = page;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.TotalCount = totalCount;

            return this.View(items);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error retrieving SQL for query title: {QueryTitle}", queryTitle);
            return this.StatusCode(500);
        }
    }
}