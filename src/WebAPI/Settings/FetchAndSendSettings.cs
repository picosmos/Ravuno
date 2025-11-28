using System.ComponentModel.DataAnnotations;
using Ravuno.DataStorage.Models;

namespace Ravuno.WebAPI.Settings;

public class FetchAndSendSettings
{
    [Required]
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    [Required]
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan FetchThreshold { get; set; } = TimeSpan.FromHours(24);

    [Required]
    [MinLength(1, ErrorMessage = "At least one source must be enabled")]
    public List<ItemSource> EnabledSources { get; set; } = [ItemSource.Tekna, ItemSource.DntActivities];
}