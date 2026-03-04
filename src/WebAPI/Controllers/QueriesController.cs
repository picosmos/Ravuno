using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[Authorize]
public class QueriesController : Controller
{
    private readonly IQueryService _queryService;
    private readonly ILogger<QueriesController> _logger;

    public QueriesController(IQueryService queryService, ILogger<QueriesController> logger)
    {
        this._queryService = queryService;
        this._logger = logger;
    }

    private int GetCurrentUserId()
    {
        return int.Parse(
            this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0",
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    [HttpGet("/queries")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = this.GetCurrentUserId();
            var queries = await this._queryService.GetAllByUserAsync(userId);
            return this.View(queries);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading queries");
            return this.StatusCode(500, "Error loading queries");
        }
    }

    [HttpGet("/queries/create")]
    public IActionResult Create()
    {
        return this.View();
    }

    [HttpPost("/queries/create")]
    public async Task<IActionResult> Create(string title, string query, string email)
    {
        try
        {
            this.ViewBag.QueryTitle = title;
            this.ViewBag.QuerySql = query;
            this.ViewBag.Email = email;

            if (string.IsNullOrWhiteSpace(title))
            {
                this.ViewBag.Error = "Title is required";
                return this.View();
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                this.ViewBag.Error = "Query is required";
                return this.View();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                this.ViewBag.Error = "Email is required";
                return this.View();
            }

            if (!this._queryService.ValidateSelectOnlyQuery(query, out var validationError))
            {
                this.ViewBag.Error = validationError;
                return this.View();
            }

            var userId = this.GetCurrentUserId();

            if (await this._queryService.TitleExistsForUserAsync(title, userId))
            {
                this.ViewBag.Error = $"A query with title '{title}' already exists";
                return this.View();
            }

            await this._queryService.CreateAsync(title, query, email, userId);
            this._logger.LogInformation("User {UserId} created query '{Title}'", userId, title);
            return this.RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error creating query");
            this.ViewBag.Error = "Error creating query: " + ex.Message;
            this.ViewBag.QueryTitle = title;
            this.ViewBag.QuerySql = query;
            return this.View();
        }
    }

    [HttpGet("/queries/edit/{id}")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            var userId = this.GetCurrentUserId();
            var query = await this._queryService.GetByIdAsync(id, userId);

            if (query == null)
            {
                this._logger.LogWarning(
                    "User {UserId} attempted to edit non-existent or unauthorized query {QueryId}",
                    userId,
                    id
                );
                return this.NotFound("Query not found");
            }

            return this.View(query);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading query {QueryId} for editing", id);
            return this.StatusCode(500, "Error loading query");
        }
    }

    [HttpPost("/queries/edit/{id}")]
    public async Task<IActionResult> Edit(long id, string title, string query, string email)
    {
        try
        {
            var userId = this.GetCurrentUserId();
            var existingQuery = await this._queryService.GetByIdAsync(id, userId);

            if (existingQuery == null)
            {
                this._logger.LogWarning(
                    "User {UserId} attempted to edit non-existent or unauthorized query {QueryId}",
                    userId,
                    id
                );
                return this.NotFound("Query not found");
            }

            this.ViewBag.Id = id;
            this.ViewBag.QueryTitle = title;
            this.ViewBag.QuerySql = query;
            this.ViewBag.Email = email;

            if (string.IsNullOrWhiteSpace(title))
            {
                this.ViewBag.Error = "Title is required";
                return this.View(existingQuery);
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                this.ViewBag.Error = "Query is required";
                return this.View(existingQuery);
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                this.ViewBag.Error = "Email is required";
                return this.View(existingQuery);
            }

            if (!this._queryService.ValidateSelectOnlyQuery(query, out var validationError))
            {
                this.ViewBag.Error = validationError;
                return this.View(existingQuery);
            }

            if (await this._queryService.TitleExistsForUserAsync(title, userId, id))
            {
                this.ViewBag.Error = $"A query with title '{title}' already exists";
                return this.View(existingQuery);
            }

            await this._queryService.UpdateAsync(id, title, query, email, userId);
            this._logger.LogInformation("User {UserId} updated query {QueryId}", userId, id);
            return this.RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error updating query {QueryId}", id);
            var userId = this.GetCurrentUserId();
            var existingQuery = await this._queryService.GetByIdAsync(id, userId);
            this.ViewBag.Error = "Error updating query: " + ex.Message;
            this.ViewBag.Id = id;
            this.ViewBag.QueryTitle = title;
            this.ViewBag.QuerySql = query;
            return this.View(existingQuery);
        }
    }

    [HttpPost("/queries/test")]
    public async Task<IActionResult> TestQuery([FromForm] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return this.Json(new { success = false, error = "Query is required" });
            }

            if (!this._queryService.ValidateSelectOnlyQuery(query, out var validationError))
            {
                return this.Json(new { success = false, error = validationError });
            }

            var items = await this._queryService.ExecuteSqlQueryAsync(
                query,
                CancellationToken.None
            );

            return this.Json(
                new
                {
                    success = true,
                    items = items.Select(i => new
                    {
                        i.Id,
                        Source = i.Source.ToString(),
                        i.SourceId,
                        RetrievedAt = i.RetrievedAt.ToString(
                            "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                        EventStartDateTime = i.EventStartDateTime.ToString(
                            "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                        EventEndDateTime = i.EventEndDateTime.ToString(
                            "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture
                        ),
                        i.Tags,
                        i.Title,
                        i.Description,
                        i.Organizer,
                        i.Location,
                        i.Url,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error testing query");
            return this.Json(
                new { success = false, error = "Error executing query: " + ex.Message }
            );
        }
    }

    [HttpGet("/queries/delete/{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            var userId = this.GetCurrentUserId();
            var query = await this._queryService.GetByIdAsync(id, userId);

            if (query == null)
            {
                this._logger.LogWarning(
                    "User {UserId} attempted to delete non-existent or unauthorized query {QueryId}",
                    userId,
                    id
                );
                return this.NotFound("Query not found");
            }

            return this.View(query);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error loading query {QueryId} for deletion", id);
            return this.StatusCode(500, "Error loading query");
        }
    }

    [HttpPost("/queries/delete/{id}")]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        try
        {
            var userId = this.GetCurrentUserId();
            await this._queryService.DeleteAsync(id, userId);
            this._logger.LogInformation("User {UserId} deleted query {QueryId}", userId, id);
            return this.RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error deleting query {QueryId}", id);
            return this.StatusCode(500, "Error deleting query");
        }
    }
}
