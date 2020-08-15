using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Utility.Models;

namespace Utility.MailServices
{

    public interface IEmailService
    {
        Task SendEmailAsync(EmailType emailType);
        public string[] SetMailBoxes { set; }

        public Activation activation { get; set; }
    }
    public enum EmailType
    {
        passwordReset,
        activationCode
    }

    public class EmailService : IEmailService
    {
        private MimeMessage emailMessage;
        public Activation activation { get; set; }

        private static IHostingEnvironment _hostingEnvironment;

        private static string Subject(EmailType type)
        {
            switch (type)
            {
                case EmailType.passwordReset:
                    return "Password Reset";
                case EmailType.activationCode:
                    return "Activate Your Account";
                default:
                    return "Activate Your Account";
            }
        }

        private static string HtmlBody(EmailType type)
        {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            switch (type)
            {
                case EmailType.passwordReset:
                    path = Path.Combine(path, @"MailServices/HtmlThemes/passwordReset.html");
                    break;
                case EmailType.activationCode:
                    path = Path.Combine(path, @"MailServices/HtmlThemes/activation.html");
                    break;
                default:
                    path = Path.Combine(path, @"MailServices/HtmlThemes/activation.html");
                    break;
            }
            using (StreamReader reader = new StreamReader(path))
            {
                return reader.ReadToEnd();
            }
        }

        private readonly EmailConfig ec;

        public string[] SetMailBoxes
        {
            set { emailMessage.To.Add(new MailboxAddress(value[0], value[1])); }
        }

        public EmailService(IOptions<EmailConfig> emailConfig, IHostingEnvironment environment)
        {
            emailMessage = new MimeMessage();
            this.ec = emailConfig.Value;
            _hostingEnvironment = environment;
            activation = new Activation();
        }

        public async Task SendEmailAsync(EmailType emailType)
        {
            try
            {
                emailMessage.From.Add(new MailboxAddress(ec.FromName, ec.FromAddress));
                emailMessage.Subject = Subject(emailType);
                string Body = HtmlBody(emailType).Replace("{{activationCode}}", activation.ActivationCode)
                    .Replace("{{activationUrl}}", activation.ActivationURL+ "/Api/User/ActivationBackLink?code="+ activation.ActivationCode +"");
                emailMessage.Body = new TextPart(TextFormat.Html) { Text = Body };
                using (var client = new SmtpClient())
                {
                    client.LocalDomain = ec.LocalDomain;

                    await client.ConnectAsync(ec.MailServerAddress, Convert.ToInt32(ec.MailServerPort), SecureSocketOptions.Auto).ConfigureAwait(false);
                    await client.AuthenticateAsync(new NetworkCredential(ec.UserId, ec.UserPassword));
                    await client.SendAsync(emailMessage).ConfigureAwait(false);
                    await client.DisconnectAsync(true).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

}
