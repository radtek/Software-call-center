using RedFox.Shared.Interfaces;
using System;
using System.ComponentModel.Composition;

namespace RedFox.Notifications
{
    public interface INotificationMetadata
    {
        string           Name   { get; }
        string           Version { get; }
        NotificationType Type    { get; }
    }

    [MetadataAttribute, AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class NotificationMetadataAttribute : ExportAttribute, INotificationMetadata
    {
        public string Name    { get; private set; }
        public string Version { get; private set; }

        public NotificationType Type { get; private set; }

        public NotificationMetadataAttribute(string name, string version, NotificationType type) : base(typeof(IRedFox))
        {
            Name    = name;
            Version = version;
            Type    = type;
        }
    }
}
