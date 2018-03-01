using Newtonsoft.Json;
using System.Collections.Generic;

namespace RedFox.Notifications
{
    public class NotificationTargetRequest
    {
        public NotificationType Type       { get; set; }
        public string[]         Recipients { get; set; }

        /// <summary>
        /// Convert a JSON-array to a List of NotificationTargetRequest
        /// </summary>
        /// <param name="json">[ { Type: "SMS", Recipients: ["1"] }, { type: "Email", Recipients: ["a@b.com"] } ]</param>
        /// <returns></returns>
        public static List<NotificationTargetRequest> ParseJSON(string json)
        {
            return JsonConvert.DeserializeObject<List<NotificationTargetRequest>>(json);
        }
    }
}