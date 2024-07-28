using BankingAppCore.Models;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace BankingAppCore.Utilities
{
    // Implement ASP.NET Core Identity's IEmailSender to override default behavior
    public class EmailService : IEmailSender
    {
        private readonly AppSettings _appSettings;

        public EmailService(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
        }

        public Task SendEmailAsync(string toAddress, string subject, string body)
        {
            var fromAddress = _appSettings.MailAccount;
            var password = _appSettings.MailPassword;
            var smtpHost = _appSettings.SmtpHost;

            var client = new SmtpClient(smtpHost)
            {
                Port = 587,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                EnableSsl = true,
                Credentials = new NetworkCredential(fromAddress, password)
            };

            var mailMessage = new MailMessage(fromAddress, toAddress)
            {
                IsBodyHtml = true,
                Subject = subject,
                Body = body
            };

            return client.SendMailAsync(mailMessage);
        }
    }
}
