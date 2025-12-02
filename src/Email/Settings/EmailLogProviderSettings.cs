using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace Ravuno.Email.Settings;

public class EmailLogProviderSettings
{
    [Required]
    [Range(typeof(TimeSpan), "00:00:01", "23:59:59")]
    public TimeSpan WaitIntervalBeforeSend { get; set; } = TimeSpan.FromMinutes(10);

    [Required]
    [Range(typeof(TimeSpan), "00:00:01", "23:59:59")]
    public TimeSpan MaxWaitTimeBeforeSend { get; set; } = TimeSpan.FromHours(1);

    [Required]
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    public bool IsEnabled { get; set; } = false;

    [Required]
    [EmailAddress]
    public string AdminEmailReceiver { get; set; } = string.Empty;

    public LogLevel MinimumLogLevelToSend { get; set; } = LogLevel.Warning;
}