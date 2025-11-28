using System.ComponentModel.DataAnnotations;

namespace WebAPI.Settings;

public class CleanupSettings
{
    [Required]
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan CleanUpInterval { get; set; } = TimeSpan.FromHours(12);

    [Required]
    [Range(typeof(TimeSpan), "1.00:00:00", "3650.00:00:00")]
    public TimeSpan HistoricRetentionTimeSpan { get; set; } = TimeSpan.FromDays(30);
}
