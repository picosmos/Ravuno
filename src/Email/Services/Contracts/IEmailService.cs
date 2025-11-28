namespace Ravuno.Email.Services.Contracts;

public interface IEmailService
{
    Task SendEmailAsync(string receiver, string subject, string body);
    Task SendEmailAsync(string receiver, string subject, string body, bool isHtml);
}