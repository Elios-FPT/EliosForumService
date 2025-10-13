using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IAppConfiguration _appConfiguration;

        public EmailService(IAppConfiguration appConfiguration)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
        }

        public async Task<IEnumerable<EmailDataDto>> GetEmailsFromSenderAsync(string sender)
        {
            if (string.IsNullOrWhiteSpace(sender))
            {
                throw new ArgumentException("Sender cannot be null or empty.", nameof(sender));
            }

            var emailConfig = _appConfiguration.GetEmailConfiguration();
            var emails = new List<EmailDataDto>();

            using var imapClient = new ImapClient();
            try
            {
                await imapClient.ConnectAsync("imap.gmail.com", 993, true);
                await imapClient.AuthenticateAsync(emailConfig.TrackingEmail, emailConfig.TrackingPassword);

                var inbox = imapClient.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                var query = SearchQuery.FromContains(sender);
                var uids = await inbox.SearchAsync(query);

                foreach (var uid in uids)
                {
                    var message = await inbox.GetMessageAsync(uid);
                    var EmailDataDto = new EmailDataDto
                    {
                        Subject = message.Subject,
                        Body = message.TextBody ?? message.HtmlBody,
                        ReceivedDate = message.Date.UtcDateTime,
                        From = message.From.ToString()
                    };
                    emails.Add(EmailDataDto);
                }

                return emails;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting email: {ex.Message}", ex);
            }
            finally
            {
                if (imapClient.IsConnected)
                {
                    await imapClient.DisconnectAsync(true);
                }
            }
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentException("Recipient address cannot be null or empty.", nameof(to));
            }
            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentException("Subject cannot be null or empty.", nameof(subject));
            }
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("Body cannot be null or empty.", nameof(body));
            }

            var emailMessage = CreateEmailMessage(to, subject, body);
            await SendAsync(emailMessage);
        }

        private MimeMessage CreateEmailMessage(string to, string subject, string body)
        {
            var emailMessage = new MimeMessage();
            var emailConfig = _appConfiguration.GetEmailConfiguration();

            emailMessage.From.Add(new MailboxAddress("HopeBox", emailConfig.From));
            emailMessage.To.Add(MailboxAddress.Parse(to));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

            return emailMessage;
        }

        private async Task SendAsync(MimeMessage emailMessage)
        {
            if (emailMessage == null)
            {
                throw new ArgumentNullException(nameof(emailMessage));
            }

            using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
            try
            {
                var emailConfig = _appConfiguration.GetEmailConfiguration();
                await smtpClient.ConnectAsync(emailConfig.SmtpServer, emailConfig.Port, true);
                await smtpClient.AuthenticateAsync(emailConfig.Username, emailConfig.Password);
                await smtpClient.SendAsync(emailMessage);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error sending email: {ex.Message}", ex);
            }
            finally
            {
                if (smtpClient.IsConnected)
                {
                    await smtpClient.DisconnectAsync(true);
                }
            }
        }
    }
}