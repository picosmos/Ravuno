using System.Text.RegularExpressions;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Services;

public partial class HtmlService : IHtmlService
{
    public string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var htmlWithLinebreaks = HtmlLineBreaksRegex()
            .Replace(html.Replace("\r", "").Replace("\n", ""), "\n");
        var text = HtmlTagsRegex().Replace(htmlWithLinebreaks, string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        return MultipleLineBreaksRegex().Replace(text, "\n").Trim();
    }

    [GeneratedRegex(@"<.*?>")]
    private static partial Regex HtmlTagsRegex();

    [GeneratedRegex(@"<(/?)(br|p)[^>]*>")]
    private static partial Regex HtmlLineBreaksRegex();

    [GeneratedRegex(@"(\n\s*)+")]
    private static partial Regex MultipleLineBreaksRegex();
}
