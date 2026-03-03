using System.Net;
using System.Net.Mail;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ravuno.Email.Services.Contracts;
using Ravuno.Email.Settings;

namespace Ravuno.Email.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService>? _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(emailSettings);

        this._emailSettings = emailSettings.Value;
        this._logger = logger;
    }

    public async Task SendEmailAsync(string receiver, string subject, string body)
    {
        await this.SendEmailAsync(receiver, subject, body, false);
    }

    public async Task SendEmailAsync(string receiver, string subject, string body, bool isHtml)
    {
        var attempt = 0;
        var maxAttempts = this._emailSettings.MaxRetryAttempts + 1; // +1 for initial attempt
        Exception? lastException = null;

        while (attempt < maxAttempts)
        {
            try
            {
                if (attempt > 0)
                {
                    // Calculate exponential backoff delay: initialDelay * 2^(attempt-1)
                    var delayMs = this._emailSettings.InitialRetryDelayMs * (1 << (attempt - 1));
                    this._logger?.LogWarning(
                        "Retrying email send (attempt {Attempt}/{MaxAttempts}) after {DelayMs}ms delay to {Receiver}",
                        attempt + 1,
                        maxAttempts,
                        delayMs,
                        receiver
                    );
                    await Task.Delay(delayMs);
                }

                using var smtpClient = new SmtpClient(
                    this._emailSettings.SmtpHost,
                    this._emailSettings.SmtpPort
                )
                {
                    EnableSsl = this._emailSettings.EnableSsl,
                    Credentials = new NetworkCredential(
                        this._emailSettings.SmtpUsername,
                        this._emailSettings.SmtpPassword
                    ),
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(
                        this._emailSettings.FromEmail,
                        this._emailSettings.FromName
                    ),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml,
                };

                mailMessage.To.Add(receiver);

                await smtpClient.SendMailAsync(mailMessage);

                if (attempt > 0)
                {
                    this._logger?.LogInformation(
                        "Email successfully sent to {Receiver} after {Attempt} retry attempts",
                        receiver,
                        attempt
                    );
                }

                return; // Success - exit the retry loop
            }
            catch (SmtpException ex)
            {
                lastException = ex;
                var isTransientError = IsTransientSmtpError(ex);

                if (!isTransientError || attempt == maxAttempts - 1)
                {
                    // Either a permanent error or we've exhausted all retries
                    this._logger?.LogError(
                        ex,
                        "Failed to send email to {Receiver} after {Attempt} attempts. Error: {ErrorMessage}",
                        receiver,
                        attempt + 1,
                        ex.Message
                    );
                    throw;
                }

                this._logger?.LogWarning(
                    ex,
                    "Transient SMTP error sending email to {Receiver} (attempt {Attempt}/{MaxAttempts}): {ErrorMessage}",
                    receiver,
                    attempt + 1,
                    maxAttempts,
                    ex.Message
                );

                attempt++;
            }
            catch (Exception ex)
            {
                this._logger?.LogError(
                    ex,
                    "Unexpected error sending email to {Receiver}: {ErrorMessage}",
                    receiver,
                    ex.Message
                );
                throw;
            }
        }

        // If we exit the loop without success, throw the last exception while preserving stack trace
        if (lastException != null)
        {
            ExceptionDispatchInfo.Capture(lastException).Throw();
        }

        throw new InvalidOperationException("Failed to send email after all retry attempts");
    }

    /// <summary>
    /// Determines if an SMTP error is transient and should be retried.
    /// </summary>
    private static bool IsTransientSmtpError(SmtpException ex)
    {
        // Check SMTP status codes for transient errors
        // 4xx codes are generally transient, 5xx are permanent
        var statusCode = ex.StatusCode;

        // Common transient error codes:
        // - ServiceNotAvailable (421): Service not available, closing channel
        // - MailboxBusy (450): Mailbox unavailable
        // - LocalErrorInProcessing (451): Local error in processing
        // - InsufficientStorage (452): Insufficient system storage
        // - ClientNotPermitted (454): Temporary authentication failure (like "454 4.3.0 Try again later")
        return statusCode == SmtpStatusCode.ServiceNotAvailable
            || statusCode == SmtpStatusCode.MailboxBusy
            || statusCode == SmtpStatusCode.LocalErrorInProcessing
            || statusCode == SmtpStatusCode.InsufficientStorage
            || statusCode == SmtpStatusCode.ClientNotPermitted
            || ex.Message.Contains("454", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Try again later", StringComparison.OrdinalIgnoreCase);
    }
}
