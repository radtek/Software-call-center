using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.DataProtection;
using RedFox.WebAPI.Database;
using RedFox.WebAPI.Models;
using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Security;

namespace RedFox.WebAPI
{
    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUserManager(IUserStore<ApplicationUser> store) : base(store)
        { }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));
            var provider = new DpapiDataProtectionProvider("WebAPI");
            manager.UserTokenProvider = new DataProtectorTokenProvider<ApplicationUser>(provider.Create("ResetPasswordPurpose"));
            //manager.RegisterTwoFactorProvider("Email Code", new EmailTokenProvider<ApplicationUser>
            //{
            //    Subject = "Security Code",
            //    BodyFormat = "Your security code is {0}"
            //});
            manager.EmailService = new EmailService();
            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider =
                    new DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider.Create("ASP.NET Identity"));
            }
            return manager;
        }
    }

    internal class EmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            //TODO : Email SMTP serever config here.
            var SmtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            int SmtpPort = Convert.ToInt32(ConfigurationManager.AppSettings["SmtpPort"]);
            var SmtpUsername = ConfigurationManager.AppSettings["SmtpUsername"];
            var SmtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
            bool SmtpEnableSsl = Convert.ToBoolean(ConfigurationManager.AppSettings["SmtpEnableSsl"]);
            string EmailFrom = ConfigurationManager.AppSettings["EmailFrom"];

            //TestCrentials            

            var client = new SmtpClient(SmtpHost, SmtpPort)
            {
                Credentials = new NetworkCredential(SmtpUsername, SmtpPassword),
                EnableSsl = SmtpEnableSsl
            };

            MailMessage mail = new MailMessage(EmailFrom, message.Destination, message.Subject, message.Body);
            mail.IsBodyHtml = true;            
            client.Send(mail);
            return Task.FromResult(0);
        }
    }   
}
