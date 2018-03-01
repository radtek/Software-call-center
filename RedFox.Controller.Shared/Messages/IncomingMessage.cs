using log4net;

using Newtonsoft.Json.Linq;

using RedFox.Core;
using RedFox.Shared;

using System;
using System.Linq;
using System.Reflection;

namespace RedFox.Controller.Messages
{
    public class IncomingMessage
    {
        private static ILog   systemLog  = LogManager.GetLogger("System");
        private static ILog   sessionLog = LogManager.GetLogger("Session");
        private static string LOG_PREFIX;

        static IncomingMessage()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name     = assembly.GetName();

            LOG_PREFIX = string.Format("Messages [{0} {1}]", name.Name, name.Version);
        }


        public void Handle(string sender, string message)
        {
            var json   = JObject.Parse(message);
            var type   = json["Type"].ToString();
            var method = GetType().GetMethod(type, BindingFlags.NonPublic | BindingFlags.Instance);

            systemLog.Debug($"{ LOG_PREFIX } Incoming { type } from { sender }");
            
            if (method != null)
            {
                method.Invoke(this, new[] { json });
            }
            else
            {
                systemLog.WarnFormat($"{ LOG_PREFIX } Message has an invalid type: {0}", type);
            }
        }

        /// <summary>
        /// Request from Server to register server
        /// </summary>
        /// <param name="json"></param>
        private async void RegisterServerRequest(JObject json)
        {
            var entities = new Entities();
            var info     = json["Server"];
            var IPv4     = info["IPv4"].ToString();
            var IPv6     = info["IPv6"].ToString();
            var server   = entities.Servers.FirstOrDefault(t => t.IPv4 == IPv4 || t.IPv6 == IPv6);

            if (server == null)
            {
                server = new Core.Server();

                entities.Servers.Add(server);
            }

            server.Active      = true;
            server.Seen        = DateTime.UtcNow;
            server.Name        = info["Name"].ToString();
            server.TTL         = info["TTL"].Value<int>();
            server.IPv4        = info["IPv4"].ToString();
            server.IPv6        = info["IPv6"].ToString();
            server.MaxSessions = info["MaxSessions"].Value<int>();
            server.Extensions.Clear();

            var extensions = info["Extensions"];

            foreach (var extension in extensions)
            {
                var name      = extension["Name"].Value<string>();
                var version   = extension["Version"].Value<string>();
                var type      = (ExtensionType) Enum.Parse(typeof(ExtensionType), extension["Type"].Value<string>(), true);

                if (type == ExtensionType.Consumer)
                {
                    var direction = (Direction) Enum.Parse(typeof(Direction), extension["Direction"].Value<string>(), true);
                }

                var entity    = entities.Extensions.SingleOrDefault(t => 
                    t.Name    == name    && 
                    t.Version == version && 
                    t.Type    == type);

                if (entity == null)
                {
                    entity = new Extension()
                    {
                        Name    = name,
                        Version = version,
                        Type    = type
                    };

                    server.Extensions.Add(entity);
                }
            }

            await entities.SaveChangesAsync();

            systemLog.Info($"{ LOG_PREFIX } Registered server { server.Id } ({ server.Name })");

            var messageClient = RedFox.Shared.Extensions.Instance.MessageClient.Value;
            var message       = new Composer().RegisterServerResponse(server);

            messageClient.Send(server.IPv4, message);

            systemLog.Debug($"{ LOG_PREFIX } Registration confirmation sent to { server.IPv4  }");
        }

        /// <summary>
        /// Request from Server to unregister server
        /// </summary>
        /// <param name="json"></param>
        private async void UnregisterServerRequest(JObject json)
        {
            var entities = new Entities();
            var info     = json["Server"];
            var id       = info["Id"].Value<int>();
            var server   = entities.Servers.Find(id);

            if (server != null)
            {
                server.Active = false;
                server.Seen   = DateTime.Now;

                entities.Servers.Attach(server);
            }

            await entities.SaveChangesAsync();

            systemLog.Debug($"{ LOG_PREFIX } Removed server { server.Id } ({ server.Name })");
        }

        /// <summary>
        /// Request from Server to update keepalive status
        /// </summary>
        /// <param name="json"></param>
        private async void KeepAliveRequest(JObject json)
        {
            var entities = new Entities();
            var info     = json["Server"];
            var count    = info["SessionCount"].Value<int>();
            var id       = info["Id"].Value<int>();
            var server   = await entities.Servers.FindAsync(id);

            if (server != null)
            {
                server.Seen   = DateTime.UtcNow;
                server.Active = true;
            }

            await entities.SaveChangesAsync();

            systemLog.Debug($"{ LOG_PREFIX } Server { server.Id } ({ server.Name }) is alive");
        }

        /// <summary>
        /// Request from Server to register a new session
        /// </summary>
        /// <param name="json"></param>
        private async void SessionStartRequest(JObject json)
        {
            var session = json["Session"].ToObject<Session>();

            using (var entities = new Entities())
            {
                var rate = entities.Rates.OrderByDescending(r => r.Valid).FirstOrDefault(r => r.Valid <= DateTime.Now)?.Id;

                if (rate.HasValue)
                    session.RateId = rate.Value;
                
                entities.Sessions.Add(session);

                await entities.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Request from Server to end an existing session
        /// </summary>
        /// <param name="json"></param>
        private async void SessionEndRequest(JObject json)
        {
            var session = json["Session"].ToObject<Session>();

            using (var entities = new Entities())
            {
                var dboject = entities.Sessions.Find(session.Id);

                if (dboject == null)
                    return;

                dboject.EndTime   = session.EndTime;
                dboject.WordCount = session.WordCount;
                dboject.State     = session.State;

                entities.Sessions.Attach(dboject);

                entities.Entry(dboject).Property(p => p.EndTime).IsModified   = true;
                entities.Entry(dboject).Property(p => p.State).IsModified     = true;
                entities.Entry(dboject).Property(p => p.WordCount).IsModified = false;

                await entities.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Response from Server to inform that changes were made to an active session
        /// </summary>
        /// <param name="json"></param>
        private async void SessionChangeResponse(JObject json)
        {
            var entities  = new Entities();
            var sessionId = json["Session"]["Id"].Value<int>();
            var session   = entities.Sessions.Find(sessionId);

            if (session == null)
            {
                sessionLog.ErrorFormat($"{ LOG_PREFIX } Cannot store changes to session {0}: Not Found ", sessionId);
            }
            
            session.Muted  = json["Session"]["Muted"].Value<bool>();
            session.Paused = json["Session"]["Paused"].Value<bool>();
            session.Record = json["Session"]["Record"].Value<bool>();

            entities.Sessions.Attach(session);

            entities.Entry(session).Property(p => p.Muted).IsModified     = true;
            entities.Entry(session).Property(p => p.Paused).IsModified    = true;
            entities.Entry(session).Property(p => p.Record).IsModified    = true;
            entities.Entry(session).Property(p => p.WordCount).IsModified = false;

            await entities.SaveChangesAsync();
        }
    }
}
