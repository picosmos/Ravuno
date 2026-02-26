using System.ComponentModel.DataAnnotations;

namespace Ravuno.Email.Settings;

public class EmailSettings
{
    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    [Required]
    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [Required]
    public string SmtpUsername { get; set; } = string.Empty;

    [Required]
    public string SmtpPassword { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for sending emails when temporary failures occur.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before the first retry attempt.
    /// Subsequent retries will use exponential backoff.
    /// </summary>
    [Range(100, 60000)]
    public int InitialRetryDelayMs { get; set; } = 2000;
}
