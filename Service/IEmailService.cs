using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using food_order_system1.Modles;
using Microsoft.Extensions.Options;

namespace food_order_system1.Service
{
    public interface IEmailService
    {
        Task SendEmailAsync(string ToEmail, string Subject, string Body);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSetting _emailSettings;

        public EmailService(IOptions<EmailSetting> emailsetting)
        {
            _emailSettings = emailsetting.Value;
        }
        public async Task SendEmailAsync(string ToEmail, string Subject, string Body)
        {
            var smtpClient = new SmtpClient(_emailSettings.SmtpHost)
            {
                Port = _emailSettings.SmtpPort,
                Credentials = new NetworkCredential(
                 _emailSettings.SmtpUser,
                _emailSettings.SmtpPass// App Password
             ),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.FromEmail),
                Subject = Subject,
                Body = Body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(ToEmail);

            await smtpClient.SendMailAsync(mailMessage);

        }
    }
}