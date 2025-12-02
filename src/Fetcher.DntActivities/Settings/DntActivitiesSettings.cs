using System.ComponentModel.DataAnnotations;
using Ravuno.Core.Validation;

namespace Ravuno.Fetcher.DntActivities.Settings;

public class DntActivitiesSettings
{
    [Required]
    [Url]
    [FormatString(1)]
    public string ActivitiesApiUrl { get; set; } = string.Empty;

    [Required]
    [Url]
    [FormatString(1)]
    public string EventDetailApiUrl { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}