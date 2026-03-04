using System.Globalization;
using System.Text;
using Ravuno.DataStorage.Models;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Services;

public class IcsService : IIcsService
{
    private readonly IHtmlService _htmlService;

    public IcsService(IHtmlService htmlService)
    {
        this._htmlService = htmlService;
    }

    public string BuildIcsFeed(List<Item> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Ravuno//Calendar Feed//EN");
        foreach (var item in items)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"UID:{item.Source}-{item.SourceId}@ravuno"
            );
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"DTSTART:{item.EventStartDateTime.ToUniversalTime():yyyyMMddTHHmmssZ}"
            );
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"DTEND:{item.EventEndDateTime.ToUniversalTime():yyyyMMddTHHmmssZ}"
            );
            sb.AppendLine("TRANSP:TRANSPARENT");
            sb.AppendLine(CultureInfo.InvariantCulture, $"SUMMARY:{EscapeIcs(item.Title)}");
            var organizer = string.IsNullOrWhiteSpace(item.Organizer)
                ? "Ravuno"
                : $"Ravuno: {item.Organizer}";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"ORGANIZER;CN={EscapeIcs(organizer)}:MAILTO:no-reply@ravuno.local"
            );
            sb.AppendLine(CultureInfo.InvariantCulture, $"LOCATION:{EscapeIcs(item.Location)}");

            var description = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(item.Url))
            {
                description.AppendLine(item.Url);
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                var strippedDescription = this._htmlService.StripHtmlTags(item.Description);
                description.AppendLine(strippedDescription);
            }

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"DESCRIPTION:{EscapeIcs(description.ToString())}"
            );
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
