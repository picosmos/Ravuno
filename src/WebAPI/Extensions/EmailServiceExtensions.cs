using System.Globalization;
using System.Text;
using Ravuno.DataStorage.Models;
using Ravuno.Email.Services.Contracts;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Extensions;

public static class EmailServiceExtensions
{
    /// <summary>
    /// Sends an email with formatted item updates (new and updated items).
    /// </summary>
    public static async Task SendItemUpdateEmailAsync(
        this IEmailService emailService,
        IHtmlService htmlService,
        string receiverAddress,
        string queryTitle,
        List<Item> newItems,
        List<Item> updatedItems
    )
    {
        ArgumentNullException.ThrowIfNull(emailService);
        ArgumentNullException.ThrowIfNull(htmlService);
        ArgumentNullException.ThrowIfNull(newItems);
        ArgumentNullException.ThrowIfNull(updatedItems);

        var sortedNewItems = newItems.OrderBy(item => item.EventStartDateTime).ToList();
        var sortedUpdatedItems = updatedItems.OrderBy(item => item.EventStartDateTime).ToList();

        var emailBody = BuildEmailBody(htmlService, sortedNewItems, sortedUpdatedItems);
        var subject = $"[Ravuno] {queryTitle} ({newItems.Count} new, {updatedItems.Count} updated)";

        await emailService.SendEmailAsync(receiverAddress, subject, emailBody, isHtml: true);
    }

    private static string BuildEmailBody(
        IHtmlService htmlService,
        List<Item> newItems,
        List<Item> updatedItems
    )
    {
        void RenderTable(StringBuilder sb, string heading, List<Item> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"<h2>{heading}</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th style=\"min-width: 20em;\">Title</th>");
            sb.AppendLine("<th style=\"min-width: 5em;\">When?<br/>(Enrollment Deadline)</th>");
            sb.AppendLine("<th style=\"min-width: 5em;\">Location</th>");
            sb.AppendLine("<th style=\"min-width: 5em;\">Price</th>");
            sb.AppendLine("</tr>");

            foreach (var item in items)
            {
                var description = htmlService.StripHtmlTags(item.Description ?? "");
                if (description.Length > 2500)
                {
                    description = string.Concat(description.AsSpan(0, 2500), "...");
                }

                description = description.Replace("\n", "<br/>");
                var tags = item.Tags != null ? string.Join(", ", item.Tags) : "";

                sb.AppendLine("<tr>");
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"<td><a href=\"{item.Url}\">{item.Title}</a></td>"
                );
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"<td>{item.EventStartDateTime:ddd, yyyy-MM-dd HH:mm} to<br/>{item.EventEndDateTime:ddd, yyyy-MM-dd HH:mm}<br/>({item.EnrollmentDeadline:ddd, yyyy-MM-dd HH:mm})</td>"
                );
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"<td>{item.Location?.Replace("\n", "<br/>")}</td>"
                );
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"<td>{item.Price?.Replace("\n", "<br/>")}</td>"
                );
                sb.AppendLine("</tr>");
                sb.AppendLine("<tr>");
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"<td colspan=\"5\" class=\"description\">{description}<br/>{tags}</td>"
                );
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
        }

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Trebuchet MS', Arial, sans-serif; }");
        sb.AppendLine("h2 { color: #333; }");
        sb.AppendLine("table { border-collapse: collapse; min-width: 100%; margin-bottom: 20px; }");
        sb.AppendLine(
            "th, td { border: 1px solid #ddd; padding: 2px 6px 2px 6px; text-align: left; vertical-align: top; }"
        );
        sb.AppendLine("th { background-color: #4CAF50; color: white; }");
        sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
        sb.AppendLine("a { color: #1a73e8; text-decoration: none; }");
        sb.AppendLine(".description { font-size: 0.9em; padding-bottom: 8px; color: #555; }");
        sb.AppendLine(
            ".tags { font-size: 0.85em; margin-top: 4px; color: #777; font-style: italic; }"
        );
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(
            "<p><em>All timestamps are given as provided by the data sources. Usually local time.</em></p>"
        );

        RenderTable(sb, "New Items", newItems);
        RenderTable(sb, "Updated Items", updatedItems);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
