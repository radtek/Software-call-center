using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RedFox.Core;

using System;
using System.Collections.Generic;
using System.Net;

namespace RedFox.Server.Messages
{
    public class Composer
    {
        private static ILog logger = LogManager.GetLogger("System");

        /// <summary>
        /// Request from Server to Controller to register server
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public string RegisterServerRequest(JObject config)
        {
            var dictionary = (IDictionary<string, JToken>)config["Server"];

            if (!dictionary.ContainsKey("Extensions") || !dictionary["Extensions"].HasValues)
            {
                throw new Exception("Cannot register a server with zero extensions");
            }

            var entry       = Dns.GetHostEntryAsync(string.Empty).Result;
            var addressList = entry.AddressList;

            if (!dictionary.ContainsKey("IPv4") || dictionary["IPv4"].ToString() == "")
            {
                var addresses = Array.FindAll(addressList, a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                if (addresses.Length > 0)
                {
                    logger.Debug("No IPv4 set in config; using " + addresses[0].ToString());
                    config["Server"]["IPv4"] = addresses[0].ToString();
                }
                else
                {
                    logger.Debug("No IPv4 set in config; IPv4 disabled");
                }
            }

            if (!dictionary.ContainsKey("IPv6") || dictionary["IPv6"].ToString() == "")
            {
                var addresses = Array.FindAll(addressList, a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);

                if (addresses.Length > 0)
                {
                    logger.Debug("No IPv6 set in config; using " + addresses[0].ToString());
                    config["Server"]["IPv6"] = addresses[0].ToString();
                }
                else
                {
                    logger.Debug("No IPv6 set in config; IPv6 disabled");
                }
            }

            if (!dictionary.ContainsKey("Name") || dictionary["Name"].ToString() == "")
            {
                config["Server"]["Name"] = entry.HostName;
            }

            if (!dictionary.ContainsKey("TTL") || dictionary["TTL"].Value<int>() <= 0)
            {
                logger.Warn("Server TTL is 0 or not set; keepalive disabled");
            }

            config["Type"] = "RegisterServerRequest";

            return JsonConvert.SerializeObject(config);
        }

        /// <summary>
        /// Request from Server to Controller to unregister server
        /// </summary>
        /// <param name="serverId"></param>
        /// <returns></returns>
        public string UnregisterServerRequest(int serverId)
        {
            dynamic json = new JObject();

            json.Type   = "UnregisterServerRequest";
            json.Server = new JObject();
            json.Server.Id = serverId;

            return JsonConvert.SerializeObject(json);
        }

        /// <summary>
        /// Request from Server to Controller to update keepalive
        /// </summary>
        /// <returns></returns>
        public string KeepAliveRequest(int id)
        {
            dynamic json = new JObject();

            json.Type   = "KeepAliveRequest";
            json.Server = new JObject();

            json.Server.Id           = id;
            json.Server.SessionCount = new SessionManager().Count;

            return JsonConvert.SerializeObject(json);
        }

        /// <summary>
        /// Request from Server to Controller to register a new session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public string SessionStartRequest(Session session)
        {
            dynamic json = new JObject();

            json.Type    = "SessionStartRequest";
            json.Session = JObject.FromObject(session); //JsonConvert.SerializeObject(session);

            return JsonConvert.SerializeObject(json);
        }

        /// <summary>
        /// Request from Server to Controller to end an existing session
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public string SessionEndRequest(Session session)
        {
            dynamic json = new JObject();

            json.Type    = "SessionEndRequest";
            json.Session = JObject.FromObject(session);
            
            return JsonConvert.SerializeObject(json);
        }

        /// <summary>
        /// Response from Server to inform that changes were made to an active session
        /// </summary>
        /// <returns></returns>
        public string SessionChangeResponse(Session session)
        {
            dynamic json = new JObject();

            json.Type = "SessionChangeResponse";

            json.Session = new JObject();
            json.Session.Id     = session.Id;
            json.Session.Muted  = session.Muted;
            json.Session.Paused = session.Paused;
            json.Session.Record = session.Record;

            return JsonConvert.SerializeObject(json);
        }
    }
}