using System.ComponentModel.DataAnnotations;

namespace Ravuno.WebAPI.Settings;

public class FetchAndSendSettings
{
    [Required]
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    [Required]
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan FetchThreshold { get; set; } = TimeSpan.FromHours(24);
}
