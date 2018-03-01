using RedFox.Shared;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RedFox.Notifications
{
    public class Notification
    {
        public string Subject { get; set; }
        public string Message { get; set; }

        public log4net.Core.Level Level { get; set; }

        public List<NotificationTargetRequest> TargetRequests { get; }

        public Notification()
        {
            TargetRequests = new List<NotificationTargetRequest>();
        }

        public void Send()
        {
            // We don't want the main process to wait until the notification is sent to all targets ...
            Task.Run(() => SendPrivate());
        }

        private void SendPrivate()
        {
            var selves = Extensions.Instance.NotificationTargets.Where(t => t.Metadata.Type == NotificationType.Self);
            var id     = 0;

            foreach (var self in selves)
            {
                self.Value.Level   = Level;
                self.Value.Subject = Subject;
                self.Value.Message = Message;
                self.Value.Send();

                id = self.Value.ID;
            }

            foreach (var request in TargetRequests)
            {
                var targets = Extensions.Instance.NotificationTargets.Where(t => t.Metadata.Type == request.Type);

                foreach (var target in targets)
                {
                    target.Value.Recipients = request.Recipients;
                    target.Value.Level      = Level;
                    target.Value.Subject    = Subject;
                    target.Value.Message    = Message;
                    target.Value.ID         = id;
                    target.Value.Send();
                }
            }
        }
    }
}
