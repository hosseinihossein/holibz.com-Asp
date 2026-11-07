using MimeKit;
using MailKit.Net.Smtp;

namespace AspApp.Helpers
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string receiverUserName, string receiverEmail, string subject, string message);
        void SendEmail(string receiverUserName, string receiverEmail, string subject, string message);
    }

    public class EmailSender : IEmailSender
    {
        //private readonly IConfiguration configuration;
        private readonly string _sourceAddress;
        private readonly string _sourcePassword;
        private const string _sourceName = "no-reply";
        public EmailSender(IConfiguration configuration)
        {
            _sourceAddress = configuration["EmailSender:Address"]!;
            _sourcePassword = configuration["EmailSender:Password"]!;
        }

        public async Task SendEmailAsync(string receiverUserName, string receiverEmail,
        string subject, string message)
        {
            MimeMessage email = new MimeMessage();
            email.From.Add(new MailboxAddress(_sourceName, _sourceAddress));
            email.To.Add(new MailboxAddress(receiverUserName, receiverEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = message,
            };

            using (SmtpClient smtp = new SmtpClient())
            {
                await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.Auto);
                await smtp.AuthenticateAsync(_sourceAddress, _sourcePassword);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }

        }

        public void SendEmail(string receiverUserName, string receiverEmail,
        string subject, string message)
        {
            MimeMessage email = new MimeMessage();
            email.From.Add(new MailboxAddress(_sourceName, _sourceAddress));
            email.To.Add(new MailboxAddress(receiverUserName, receiverEmail));
            email.Subject = subject;
            email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = message,
            };

            using (SmtpClient smtp = new SmtpClient())
            {
                smtp.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.Auto);
                smtp.Authenticate(_sourceAddress, _sourcePassword);
                smtp.Send(email);
                smtp.Disconnect(true);
            }

        }
    }
}