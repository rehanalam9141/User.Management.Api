using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using User.Management.Service.Models;

namespace User.Management.Service.Services;

public class EmailService : IEmailService
{
    private readonly EmailConfiguration _emailConfig;
    public EmailService(EmailConfiguration emailConfig) => _emailConfig = emailConfig;
    public void SendEmail(Message message)
    {
       var emailMessage = CreateEmailMessage(message);
       Send(emailMessage);
    }

    private MimeMessage CreateEmailMessage(Message message)
    {
        var emailMessage = new MimeMessage();
        emailMessage.From.Add(new MailboxAddress("email",_emailConfig.From));
        emailMessage.To.AddRange(message.To);
        emailMessage.Subject = message.Subject;
        emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Text){Text = message.Content};
        return emailMessage;
    }

    private void Send(MimeMessage mailMessage)
    {
        using (var client = new SmtpClient())
        {
            try
            {
                // Connect to the SMTP server
                client.Connect(_emailConfig.SmtpServer, _emailConfig.Port, true);

                // Authenticate if needed
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                client.Authenticate(_emailConfig.UserName, _emailConfig.Password);

                // Send the message
                client.Send(mailMessage);

                // Disconnect
                client.Disconnect(true);
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., log the error)
                Console.WriteLine("Error sending email: " + ex.Message);
            }
            finally
            {
                client.Disconnect(true);
                client.Dispose();
            }
        }
    }
}