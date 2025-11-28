using System.Globalization;
using System.Text;
using Ravuno.Email.Services.Contracts;

namespace Ravuno.WebAPI.Extensions;

public static class EmailServiceExtensions
{
    public static async Task SendErrorNotificationAsync(this IEmailService emailService, string receiver, string title, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(emailService);

        var body = new StringBuilder();
        body.AppendLine(CultureInfo.InvariantCulture, $"<h2>{title}</h2>");
        body.AppendLine(CultureInfo.InvariantCulture, $"<p><strong>Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        body.AppendLine(CultureInfo.InvariantCulture, $"<p><strong>Exception Type:</strong> {exception?.GetType().FullName}</p>");
        body.AppendLine(CultureInfo.InvariantCulture, $"<p><strong>Message:</strong> {exception?.Message}</p>");
        body.AppendLine($"<h3>Stack Trace:</h3>");
        body.AppendLine(CultureInfo.InvariantCulture, $"<pre>{exception?.StackTrace}</pre>");

        var innerException = exception?.InnerException;
        while (innerException != null)
        {
            body.AppendLine($"<h3>Inner Exception:</h3>");
            body.AppendLine(CultureInfo.InvariantCulture, $"<p><strong>Type:</strong> {innerException.GetType().FullName}</p>");
            body.AppendLine(CultureInfo.InvariantCulture, $"<p><strong>Message:</strong> {innerException.Message}</p>");
            body.AppendLine(CultureInfo.InvariantCulture, $"<pre>{innerException.StackTrace}</pre>");
            innerException = innerException.InnerException;
        }

        await emailService.SendEmailAsync(
            receiver: receiver,
            subject: $"[ERROR] {title}",
            body: body.ToString(),
            isHtml: true
        );
    }
}