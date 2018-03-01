using log4net.Core;

namespace RedFox.Notifications
{
    public interface INotificationTarget
    {
        string[] Recipients { get; set; }

        string Subject       { get; set; }
        string Message       { get; set; }

        int   ID    { get; set; }
        Level Level { get; set; }

        void Send();
    }
}
