using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("calendar")]
public class CalendarController : ControllerBase
{
    private readonly IQueryService _queryService;
    private readonly IIcsService _icsService;

    public CalendarController(IQueryService queryService, IIcsService icsService)
    {
        this._queryService = queryService;
        this._icsService = icsService;
    }

    [AllowAnonymous]
    [HttpGet("public/{publicId}")]
    public async Task<IActionResult> GetCalendarByPublicId(string publicId)
    {
        var config = await this._queryService.GetUpdateConfigurationByPublicIdAsync(publicId);
        if (config == null)
        {
            return this.NotFound();
        }

        var items = await this._queryService.ExecuteSqlQueryAsync(config.SqlQuery);
        var ics = this._icsService.BuildIcsFeed(items);
        return this.File(Encoding.UTF8.GetBytes(ics), "text/calendar", config.QueryTitle + ".ics");
    }
}
