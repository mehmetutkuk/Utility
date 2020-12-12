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
        Task SendEmailAsync();
        public string[] SetMailBoxes { set; }
        public void SetEmailType(EmailType emailType);
        public void SetActivation(Activation activation);
        public void SetEmailMessage();
    }
    public enum EmailType
    {
        PasswordReset,
        UserActivation
    }

    public class EmailService : IEmailService
    {
        private MimeMessage emailMessage;
        public Activation _activation;
        public static EmailType _emailType;

        private static IHostingEnvironment _hostingEnvironment;
        private readonly IConfiguration _configuration;
        public EmailService( IHostingEnvironment environment, IConfiguration configuration)
        {
            emailMessage = new MimeMessage();
            ec = new EmailConfig()
            {
                FromAddress = Environment.GetEnvironmentVariable("EMAIL_FROM_ADDRESS"),
                LocalDomain = Environment.GetEnvironmentVariable("EMAIL_LOCAL_DOMAIN"),
                FromName = Environment.GetEnvironmentVariable("EMAIL_FROM_NAME"),
                MailServerAddress = Environment.GetEnvironmentVariable("EMAIL_MAIL_SERVER_ADDRESS"),
                MailServerPort = Environment.GetEnvironmentVariable("EMAIL_MAIL_SERVER_PORT"),
                UserId = Environment.GetEnvironmentVariable("EMAIL_USER_ID"),
                UserPassword = Environment.GetEnvironmentVariable("EMAIL_USER_PASSWORD")
            };
            _hostingEnvironment = environment;
            _configuration = configuration;
        }
        public void SetEmailType(EmailType emailType)
        {
            _emailType = emailType;
        }
        public void SetActivation(Activation activation)
        {
            _activation = activation;
            _activation.ActivationURL = _configuration.GetSection("Frontend").Get<UrlConfig>().Url;
        }
        private static string Subject()
        {
            switch (_emailType)
            {
                case EmailType.PasswordReset:
                    return "Password Reset";
                case EmailType.UserActivation:
                    return "Activate Your Account";
                default:
                    return "Activate Your Account";
            }
        }

        private static string HtmlBody()
        {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            switch (_emailType)
            {
                case EmailType.PasswordReset:
                    path = Path.Combine(path, @"MailServices/HtmlThemes/passwordReset.html");
                    break;
                case EmailType.UserActivation:
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
        private string ReplaceHtmlBody(string body)
        {
            body = body.Replace("{{activationCode}}", _activation.ActivationCode);
            switch (_emailType)
            {
                case EmailType.UserActivation:
                    body = body.Replace("{{activationUrl}}", _activation.ActivationURL + "/pages/activate-account?c=" + Cryptography.Cryptography.Encrypt(_activation.ActivationCode));
                    break;
                case EmailType.PasswordReset:
                    body = body.Replace("{{activationUrl}}", _activation.ActivationURL + "/pages/reset-password?c=" + Cryptography.Cryptography.Encrypt(_activation.ActivationCode));
                    break;
                default:
                    break;
            }
            return body;
        }
        private readonly EmailConfig ec;

        public string[] SetMailBoxes
        {
            set { emailMessage.To.Add(new MailboxAddress(value[0], value[1])); }
        }
        public void SetEmailMessage()
        {
            emailMessage.From.Add(new MailboxAddress(ec.FromName, ec.FromAddress));
            emailMessage.Subject = Subject();
            string Body = HtmlBody();
            Body = ReplaceHtmlBody(Body);
            emailMessage.Body = new TextPart(TextFormat.Html) { Text = Body };
        }
        public async Task SendEmailAsync()
        {
            try
            { 
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
