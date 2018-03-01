using log4net.Core;

using RedFox.Notifications;

using System.ComponentModel.Composition;

using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace RedFox.NotificationTargets.TwilioSMS
{
    [Export(typeof(INotificationTarget)), NotificationMetadata("TwilioSMS", "1.0", NotificationType.SMS)]
    public class NotificationTarget : INotificationTarget
    {
        public string[] Recipients { get; set; }

        public string Subject { get; set; }
        public string Message { get; set; }

        public int   ID    { get; set; }
        public Level Level { get; set; }

        public NotificationTarget()
        {
            TwilioClient.Init("AC5eebd30a9458729d5a75b55d48068b05", "ff4640269b27c3ec40dba004d72ffac6");
        }

        public void Send()
        {
            if (Recipients == null) return;

            foreach (var recipient in Recipients)
            { 
                MessageResource.Create(
                    to  : new PhoneNumber(recipient),
                    from: new PhoneNumber("+18582642600"),
                    body: Message
                );
            }
        }
    }
}
