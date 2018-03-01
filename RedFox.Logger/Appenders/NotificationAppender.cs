using log4net.Appender;
using log4net.Core;

using RedFox.Notifications;

namespace RedFox.Log4Net.Appenders
{
    public class NotificationAppender : AppenderSkeleton
    {
        protected override void Append(LoggingEvent loggingEvent)
        {
            var notification = new Notification
            {
                Level   = loggingEvent.Level,
                Subject = "",
                Message = loggingEvent.RenderedMessage
            };

            // Check in database which addresses this should be sent to

            //notification.TargetRequests.Add(new NotificationTargetRequest { Address = "+4747285576", Type = NotificationType.SMS });

            if (notification.TargetRequests.Count > 0)
                notification.Send();
        }
    }
}
