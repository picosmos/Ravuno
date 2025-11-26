namespace Email.Services.Contracts;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendEmailAsync(string to, string subject, string body, bool isHtml);
}