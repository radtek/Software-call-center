using log4net;

using RedFox.Core;
using RedFox.Consumers;
using RedFox.Server.Messages;
using RedFox.Shared;

using System;
using System.Collections.Generic;
using System.Linq;

namespace RedFox.Server
{
    public class SessionManager
    {
        private static ILog   logger     = LogManager.GetLogger("System");
        private static string LOG_PREFIX = "";

        private static readonly Dictionary<int, Session> sessions = new Dictionary<int, Session>();
        private static readonly Lazy<SessionManager>     instance = new Lazy<SessionManager>(() => new SessionManager());

        public static SessionManager Instance { get { return instance.Value; } }
        
        static SessionManager()
        {
            var assembly = System.Reflection.Assembly.GetCallingAssembly();
            var name     = assembly.GetName();

            LOG_PREFIX = string.Format("Session Manager [{0} {1}]", name.Name, name.Version);
        }

        public static void CreateSession(IConsumer consumer, string endpoint)
        {
            try
            { 
                var session  = new Session(consumer, endpoint);

                if (session.Station == null)
                {
                    logger.Error($"{ LOG_PREFIX } Cannot start a Session without a Station");
                    return;
                }

                // TODO Record parameter must be fetch from Station settings
                // session.Record = true;
                 session.Start();
                sessions.Add(session.Id, session);

                //messageClient.Send(
                //    "controller",
                //    new Composer().SessionStartRequest(session));
            }
            catch (Exception e)
            {
                logger.Error($"{ LOG_PREFIX } Error during Session state change; { e.Message }");
                logger.Debug(e.StackTrace);
            }
        }

        public static void CreateSession(string consumerName, string endpoint)
        {
            try
            { 
                var factory  = Extensions.Instance.Consumers.FirstOrDefault(t => t.Metadata.Name == consumerName);
                var consumer = factory.CreateExport()?.Value;

                consumer.Init();

                CreateSession(consumer, endpoint);
            }
            catch (Exception e)
            {
                logger.Error($"{ LOG_PREFIX } Error during Session state change; { e.Message }");
                logger.Debug(e.StackTrace);
            }
        }

        public static void DestroySession(int sessionId)
        {
            var messageClient = Extensions.Instance.MessageClient.Value;

            try
            {
                if (sessionId == 0 || sessions.Count == 0)
                {
                    return;
                }

                var session = sessions[sessionId];

                if (session == null)
                {
                    logger.Error($"{ LOG_PREFIX } Session { sessionId } does not exist in the server-side session list");
                    return;
                }

                    session.End();
                sessions.Remove(session.Id);

                messageClient.Send(
                    "controller",
                    new Composer().SessionEndRequest(session));
            }
            catch (Exception e)
            {
                logger.Error($"{ LOG_PREFIX } Error during Session state change; { e.Message }");
                logger.Debug(e.StackTrace);
            }
        }
        
        public void Crumble()
        {
            foreach (var session in sessions.ToArray())
            {
                try
                { 
                     session.Value.End();
                    sessions.Remove(session.Key);
                }
                catch (Exception e)
                {
                    logger.Error($"{ LOG_PREFIX } Cannot end Session { session.Value.Id }; { e.Message }");
                    logger.Debug(e.StackTrace);
                }
            }
        }

        public Dictionary<int, Session> Sessions
        {
            get { return sessions; }
        }

        public int Count
        {
            get { return sessions.Count;  }
        }
    }
}
