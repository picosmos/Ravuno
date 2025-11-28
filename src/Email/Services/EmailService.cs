using System.Net;
using System.Net.Mail;
using Ravuno.Email.Services.Contracts;
using Ravuno.Email.Settings;
using Microsoft.Extensions.Options;

namespace Ravuno.Email.Services;

public class EmailService(IOptions<EmailSettings> emailSettings) : IEmailService
{
    private readonly EmailSettings _emailSettings = emailSettings.Value;

    public async Task SendEmailAsync(string receiver, string subject, string body)
    {
        await this.SendEmailAsync(receiver, subject, body, false);
    }

    public async Task SendEmailAsync(string receiver, string subject, string body, bool isHtml)
    {
        using var smtpClient = new SmtpClient(this._emailSettings.SmtpHost, this._emailSettings.SmtpPort)
        {
            EnableSsl = this._emailSettings.EnableSsl,
            Credentials = new NetworkCredential(this._emailSettings.SmtpUsername, this._emailSettings.SmtpPassword)
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(this._emailSettings.FromEmail, this._emailSettings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };

        mailMessage.To.Add(receiver);

        await smtpClient.SendMailAsync(mailMessage);
    }
}