using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedFox.Core;
using RedFox.Shared;
using System;
using System.Linq;
using System.Reflection;

namespace RedFox.Server.Messages
{
    public class IncomingMessage
    {
        private static ILog systemLog  = LogManager.GetLogger("System");
        private static ILog sessionLog = LogManager.GetLogger("Session");

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
        /// Response from Controller to Server that registration was successful
        /// </summary>
        /// <param name="json"></param>
        private void RegisterServerResponse(JObject json)
        {
            var server = json["Server"].Value<string>();
                        
            Core.Server.Instance = JsonConvert.DeserializeObject<Core.Server>(server);
        }

        /// <summary>
        /// Request from Controller to start a new session
        /// </summary>
        /// <param name="json"></param>
        private void SessionStartRequest(JObject json)
        {
            SessionManager.CreateSessionFromJson(json);
        }

        /// <summary>
        /// Request from Controller to makes changes to an active Session
        /// </summary>
        /// <param name="json"></param>
        private void SessionChangeRequest(JObject json)
        {
            // TODO Return SessionChangeResponse
            var sessionId = json["Session"]["Id"].Value<int>();

            sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} received a change request", sessionId);

            var muted  = json["Session"]["Muted"].Value<bool>();
            var paused = json["Session"]["Paused"].Value<bool>();
            var record = json["Session"]["Record"].Value<bool>();

            var session = SessionManager.Instance.Sessions.SingleOrDefault(s => s.Key == sessionId).Value;
            
            if (session == null)
            {
                sessionLog.ErrorFormat($"{ LOG_PREFIX } Cannot make changes to session {0}: Session does not exist or is not active", sessionId);
            }
            
            var consumer = session.Consumer;
            var provider = session.Encoder.EncoderType.Provider.Value;

            if (consumer == null)
            {
                sessionLog.ErrorFormat($"{ LOG_PREFIX } Cannot access the consumer for session {0}: Consumer seems to be missing", sessionId);
            }

            if (provider == null)
            {
                sessionLog.ErrorFormat($"{ LOG_PREFIX } Cannot access the provider for session {0}: Provider seems to be missing", sessionId);
            }

            if (session.Muted != muted)
            {
                session.Muted = muted;

                if (muted)
                {
                    provider.Mute();
                    sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} provider is now muted", sessionId);
                }
                else
                {
                    provider.Unmute();
                    sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} provider is unmuted", sessionId);
                }
            }

            if (session.Paused != paused)
            {
                session.Paused = paused;

                if (paused)
                {
                    consumer.Pause();
                    sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} consumer is now paused", sessionId);
                }
                else
                {
                    consumer.Resume();
                    sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} consumer has resumed", sessionId);
                }
            }

            if (session.Record != record)
            {
                session.Record = record;

                if (record)
                {
                    session.Record = session.StartRecording();

                    if (session.Record) sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} audio is being recorded", sessionId);
                                   else sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} audio is NOT recording; check log for errors", sessionId);
                }
                else
                {
                    session.StopRecording();
                    sessionLog.DebugFormat($"{ LOG_PREFIX } Session {0} stopped recording audio", sessionId);
                }
            }

            var response      = new Composer().SessionChangeResponse(session);
            var messageClient = Shared.Extensions.Instance.MessageClient;

            if (messageClient == null)
            {
                sessionLog.Error($"{ LOG_PREFIX } The messageclient is unavailable");
            }

            messageClient.Value.Send("controller", response);
        }
    }
}
