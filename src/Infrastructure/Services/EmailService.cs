using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Infrastructure.Options;
using Serilog;
namespace SchoolMaster.Infrastructure.Services;

public class EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendEmailAsync(string email, string name, string subject, string body)
    {
        try
        {
            var smtpServer = options.Value.SmtpServer;
            var smtpPort = options.Value.SmtpPort;
            var senderEmail = options.Value.SenderEmail;
            var senderName = options.Value.SenderName;
            var password = options.Value.Password;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(name, email));
            message.Subject = subject;

            message.Body = new TextPart("plain")
            {
                Text = $@"Hey {name},

            {body}

            -- {senderName}"
            };
            using var client = new SmtpClient();
            await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);

            // Note: only needed if the SMTP server requires authentication
            await client.AuthenticateAsync(senderEmail, password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (SmtpProtocolException)
        {
            throw;
        }
        catch (SmtpCommandException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while sending the email");
            throw new Exception($"Failed to send email: {ex.Message}");
        }
    }
}