using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;
using Ravuno.DataStorage.Constants;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[Authorize]
[Route("stats")]
public class StatsController : Controller
{
    private readonly DataStorageContext _context;
    private readonly IQueryService _queryService;
    private readonly IUserService _userService;
    private readonly ILogger<StatsController> _logger;

    public StatsController(
        DataStorageContext context,
        IQueryService queryService,
        IUserService userService,
        ILogger<StatsController> logger
    )
    {
        this._context = context;
        this._queryService = queryService;
        this._userService = userService;
        this._logger = logger;
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("fetch-history")]
    public async Task<IActionResult> FetchHistory(int page = 1, int pageSize = 100)
    {
        if (page < 1)
        {
            page = 1;
        }

        var totalCount = await this._context.FetchHistories.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var histories = await this
            ._context.FetchHistories.OrderByDescending(h => h.ExecutionStartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        this.ViewBag.CurrentPage = page;
        this.ViewBag.TotalPages = totalPages;
        this.ViewBag.PageSize = pageSize;
        this.ViewBag.TotalCount = totalCount;

        return this.View(histories);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("send-update-history")]
    public async Task<IActionResult> SendUpdateHistory(int page = 1, int pageSize = 100)
    {
        if (page < 1)
        {
            page = 1;
        }

        var totalCount = await this._context.SendUpdateHistories.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var histories = await this
            ._context.SendUpdateHistories.OrderByDescending(h => h.SentAt)
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

        var items = await this
            ._context.Items.OrderByDescending(i => i.RetrievedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        this.ViewBag.CurrentPage = page;
        this.ViewBag.TotalPages = totalPages;
        this.ViewBag.PageSize = pageSize;
        this.ViewBag.TotalCount = totalCount;

        return this.View(items);
    }

    [HttpGet("view-query/{id:long}")]
    public async Task<IActionResult> ViewQuery(long id, int page = 1, int pageSize = 100)
    {
        if (page < 1)
        {
            page = 1;
        }

        try
        {
            var config = await this._queryService.GetUpdateConfigurationByIdAsync(id);

            if (config == null)
            {
                return this.NotFound();
            }

            // Execute SQL query
            var allResults = await this._queryService.ExecuteSqlQueryAsync(
                config.SqlQuery,
                this.HttpContext.RequestAborted
            );

            var totalCount = allResults.Count;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = allResults.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            this.ViewBag.QueryId = id;
            this.ViewBag.QueryTitle = config.QueryTitle;
            this.ViewBag.SqlQuery = config.SqlQuery;
            this.ViewBag.PublicId = config.PublicId;
            this.ViewBag.EmailReceiver = string.Join(", ", config.EmailReceiverAddresses);
            this.ViewBag.CurrentPage = page;
            this.ViewBag.TotalPages = totalPages;
            this.ViewBag.PageSize = pageSize;
            this.ViewBag.TotalCount = totalCount;

            return this.View(items);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error retrieving SQL for query ID: {QueryId}", id);
            return this.StatusCode(500);
        }
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpGet("all-queries")]
    public async Task<IActionResult> AllQueries()
    {
        try
        {
            var queries = await this
                ._context.Queries.Include(q => q.User)
                .OrderBy(q => q.User.Username)
                .ThenBy(q => q.Title)
                .AsNoTracking()
                .ToListAsync();

            var users = await this._userService.GetAllUsersAsync();
            this.ViewBag.Users = users;

            return this.View(queries);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error retrieving all queries");
            return this.StatusCode(500);
        }
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("all-queries/reassign")]
    public async Task<IActionResult> ReassignQueries()
    {
        try
        {
            var form = this.Request.Form;
            var reassignments = 0;

            foreach (
                var key in form.Keys.Where(k =>
                    k.StartsWith("userId_", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                var queryIdStr = key["userId_".Length..];
                if (
                    long.TryParse(queryIdStr, out var queryId)
                    && int.TryParse(form[key], out var userId)
                )
                {
                    await this._queryService.ReassignQueryAsync(queryId, userId);
                    reassignments++;
                }
            }

            var queries = await this
                ._context.Queries.Include(q => q.User)
                .OrderBy(q => q.User.Username)
                .ThenBy(q => q.Title)
                .AsNoTracking()
                .ToListAsync();

            var users = await this._userService.GetAllUsersAsync();
            this.ViewBag.Users = users;
            this.ViewBag.Success = $"Successfully reassigned {reassignments} queries";

            return this.View("AllQueries", queries);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error reassigning queries");

            var queries = await this
                ._context.Queries.Include(q => q.User)
                .OrderBy(q => q.User.Username)
                .ThenBy(q => q.Title)
                .AsNoTracking()
                .ToListAsync();

            var users = await this._userService.GetAllUsersAsync();
            this.ViewBag.Users = users;
            this.ViewBag.Error = "Error reassigning queries: " + ex.Message;

            return this.View("AllQueries", queries);
        }
    }
}
