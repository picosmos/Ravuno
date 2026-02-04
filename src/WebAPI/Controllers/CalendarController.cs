using System.Text;
using Microsoft.AspNetCore.Mvc;
using Ravuno.DataStorage.Models;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Controllers;

[ApiController]
[Route("calendar")]
public class CalendarController : ControllerBase
{
    private readonly IUpdateConfigurationService _updateConfigService;

    public CalendarController(IUpdateConfigurationService updateConfigService)
    {
        this._updateConfigService = updateConfigService;
    }

    [HttpGet("{queryTitle}")]
    public async Task<IActionResult> GetCalendar(string queryTitle)
    {
        var config = await this._updateConfigService.GetUpdateConfigurationByTitleAsync(queryTitle);
        if (config == null)
        {
            return this.NotFound();
        }

        var items = await this._updateConfigService.ExecuteSqlQueryAsync(config.SqlQuery);
        var ics = BuildIcsFeed(items);
        return this.File(Encoding.UTF8.GetBytes(ics), "text/calendar", queryTitle + ".ics");
    }

    private static string BuildIcsFeed(List<Item> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Ravuno//Calendar Feed//EN");
        foreach (var item in items)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"UID:{item.Source}-{item.SourceId}@ravuno");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"DTSTART:{item.EventStartDateTime.ToUniversalTime():yyyyMMddTHHmmssZ}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"DTEND:{item.EventEndDateTime.ToUniversalTime():yyyyMMddTHHmmssZ}");
            sb.AppendLine("TRANSP:TRANSPARENT"); // Mark as "not busy" - informational only
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"SUMMARY:{EscapeIcs(item.Title)}");
            var organizer = string.IsNullOrWhiteSpace(item.Organizer) ? "Ravuno" : $"Ravuno: {item.Organizer}";
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"ORGANIZER;CN={EscapeIcs(organizer)}:MAILTO:no-reply@ravuno.local");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"LOCATION:{EscapeIcs(item.Location)}");
            var description = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(item.Url))
            {
                description.AppendLine(item.Url);
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                description.AppendLine(item.Description);
            }

            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"DESCRIPTION:{EscapeIcs(description.ToString())}");
            sb.AppendLine("END:VEVENT");
        }
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcs(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\n", "\\n").Replace("\r", "").Replace(",", "\\,").Replace(";", "\\;");
    }
}