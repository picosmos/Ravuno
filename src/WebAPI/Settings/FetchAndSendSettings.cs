using System.ComponentModel.DataAnnotations;

namespace Ravuno.WebAPI.Settings;

public class FetchAndSendSettings
{
    [Required]
    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00")]
    public TimeSpan FetchInterval { get; set; } = TimeSpan.FromHours(24);

    [Required]
    [Range(0, int.MaxValue)]
    public int FetchDetailedEvery { get; set; } = 7;
}
