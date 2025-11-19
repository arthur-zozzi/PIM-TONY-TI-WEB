using TonyTI_Web.Models;

namespace TonyTI_Web.Services
{
    // Services/IEmailSender.cs
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string body);
    }

}
