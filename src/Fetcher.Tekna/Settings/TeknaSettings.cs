using System.ComponentModel.DataAnnotations;
using Ravuno.Core.Validation;

namespace Ravuno.Fetcher.Tekna.Settings;

public class TeknaSettings
{
    [Required]
    [Url]
    [FormatString(1)]
    public string CoursesApiUrl { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}