using log4net.Core;

using RedFox.Notifications;

using System.ComponentModel.Composition;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace RedFox.NotificationTargets.Tglrm
{
    [Export(typeof(INotificationTarget)), NotificationMetadata("Telegram", "1.0", NotificationType.SMS)]
    public class TelegramTarget : INotificationTarget
    {
        public string[] Recipients { get; set; }

        public string Subject { get; set; }
        public string Message { get; set; }

        public int   ID    { get; set; }
        public Level Level { get; set; }

        public void Send()
        {
            var msg = new Message()
            {
                Caption = Subject,
                Text    = Message,
                Contact = new Contact()
                {
                    FirstName   = "Karel",
                    LastName    = "Boek",
                    PhoneNumber = "+4747285576"
                }
            };
            
            var bot  = new TelegramBotClient("446129723:AAHE-mfbzWHq8X_1tD7YQSNT-651WAbOoe4");

        }
    }
}
