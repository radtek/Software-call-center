//using Amazon;
//using Amazon.Sns;

using log4net.Core;

using System.ComponentModel.Composition;

namespace RedFox.Notifications.Targets
{
    [Export(typeof(INotificationTarget)), NotificationMetadata("AmazonSns", "1.0", NotificationType.AmazonSNS)]
    public class AmazonSNSTarget : INotificationTarget
    {
        public string[] Recipients { get; set; }

        public string Subject { get; set; }
        public string Message { get; set; }

        public int ID { get; set; }
        public Level Level { get; set; }

        /// <summary>
        /// Topic Amazon Resource Name
        /// </summary>
        public string TopicARN { get; set; }

        /// <summary>
        /// Amazon Region
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Use access keys to make secure REST or HTTP Query protocol requests to AWS service APIs
        /// </summary>
        public string AccessKeyID { get; set; }

        /// <summary>
        /// Secret access key
        /// </summary>
        public string SecretAccessKey { get; set; }

        public void Send()
        {
            //var awsCredential  = new AwsCredential(AccessKeyID, SecretAccessKey);
            //var snsClient      = new SnsClient(AwsRegion.Get(Region), awsCredential);
            //var publishRequest = new PublishRequest()
            //    {
            //        Message  = Message,
            //        Subject  = Level.DisplayName + " notification from Red Fox",
            //        TopicArn = TopicARN
            //    };

            //snsClient.PublishAsync(publishRequest);
        }



        
        
    }
}