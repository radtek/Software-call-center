using log4net;
using log4net.Core;

using Newtonsoft.Json;

using RedFox.Consumers;
using RedFox.Converters;
using RedFox.Messaging;
using RedFox.Notifications;
using RedFox.Providers;
using RedFox.Recorders;
using RedFox.Transcribers;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RedFox.Core
{
    [MetadataType(typeof(SessionMetadata))]
    public partial class Session
    {
        private ITranscriber      transcriber;
        private ICaptionConverter converter;
        private ICaptionProvider  provider;
        private ICaptionProvider  archive;
        private IRecorder         recorder;
        //private string            endpoint;
        private bool              recording;

        private static string LOG_PREFIX;

        private List<NotificationTargetRequest> targets;

        private static ILog systemLog  = LogManager.GetLogger("System");
        private static ILog sessionLog = LogManager.GetLogger("Session");

        public IConsumer Consumer { get; private set; }

        public TimeSpan Elapsed
        {
            get
            {
                if (State == SessionState.Active   && StartTime.HasValue)
                    return DateTime.Now  - StartTime.Value;

                if (State == SessionState.Finished && StartTime.HasValue && EndTime.HasValue)
                    return EndTime.Value - StartTime.Value;

                return new TimeSpan(0);
            }
        }

        static Session()
        {
            var assemblyName = Assembly.GetCallingAssembly().GetName();

            LOG_PREFIX = string.Format("[{0} {1}]", assemblyName.Name, assemblyName.Version);
        }

        public Session()
        { }

        internal Session(IConsumer consumer, Encoder encoder)
        {
            Consumer = consumer;
            Encoder  = encoder;
            
            if (Encoder == null)
            {
                 systemLog.Error($"{ LOG_PREFIX } No Encoder specified");
                sessionLog.Error($"{ LOG_PREFIX } No Encoder specified");

                throw new NullReferenceException("No Encoder specified");
            }
            
            ServerId = Server.Instance.Id;
            targets  = new List<NotificationTargetRequest>();

            using (var entities = new Entities())
            {
                RateId = entities.Rates.OrderByDescending(r => r.Valid).FirstOrDefault(r => r.Valid < DateTime.Now).Id;
                
                Create(entities);
            }
            
            Consumer.SessionId = Id;
        }

        public async void Start()
        {
            if (!string.IsNullOrEmpty(NotificationsJson))
            {
                targets = NotificationTargetRequest.ParseJSON(NotificationsJson);
            }

            var notification = new Notification()
            {
                Level   = Level.Info,
                Subject = "Session started",
                Message = string.Format("A new session with ID {0} was started on {1}", Id, DateTime.UtcNow)
            };

            notification.TargetRequests.AddRange(targets);
            notification.Send();
                        
            systemLog.Debug($"{ LOG_PREFIX } Loading converter extension");

            try
            {
                converter = Shared.Extensions.Instance.Converters
                   .FirstOrDefault(t => t.Metadata.Name.Equals("CEA608") && t.Metadata.Version.Equals("1.0"))?
                       .Value;
            }
            catch (Exception ex)
            {
                systemLog.Error($"{ LOG_PREFIX } Could not load converter extension; { ex.Message }");
                systemLog.Debug(ex.StackTrace);
            }

            try
            {
                archive = Shared.Extensions.Instance.CaptionProviders
                    .FirstOrDefault(t => t.Metadata.Name.Equals("Archive"))
                        .Value;

                if (archive != null)
                { 
                    archive.SessionId = Id;
                    archive.Init();
                }
            }
            catch (Exception ex)
            {
                systemLog.Error(LOG_PREFIX + " Could not load archive extension", ex);
            }
            
            systemLog.Debug("Loading provider extension");

            if (Encoder.EncoderType.Provider == null)
            {
                systemLog.Error(LOG_PREFIX + " Could not load provider extension");
                return;
            }

            provider = Encoder.EncoderType.Provider.Value;

            if (provider == null)
            {
                systemLog.Error(LOG_PREFIX + " Could not load provider extension");
                return;
            }
            
            switch (Encoder.EncoderType.Protocol)
            {
                case ProtocolType.Phone:
                    provider.Endpoint = Encoder.PhoneNumber;
                    break;

                case ProtocolType.Telnet:
                    provider.Endpoint = Encoder.IpAddress + ":" + Encoder.Port;
                    break;

                default:
                    break;
            }

            provider.SessionId = Id;
            provider.Init();
            
            systemLog.Debug(LOG_PREFIX + " Loading transcriber extensions");

            try
            {
                //transcriber = Shared.Extensions.Instance.Transcribers
                //    .FirstOrDefault(t => t.Metadata.Name.Equals("Microsoft Bing") && t.Metadata.Version.Equals("1.0"))?
                //        .Value;
                transcriber = Shared.Extensions.Instance.Transcribers
                    .FirstOrDefault(t => t.Metadata.Name.Equals("Watson") && t.Metadata.Version.Equals("2.0"))?
                        .Value;
                //transcriber = Shared.Extensions.Instance.Transcribers
                //    .FirstOrDefault(t => t.Metadata.Name.Equals("Google Cloud Speech") && t.Metadata.Version.Equals("1.0"))?
                //        .Value;
            }
            catch (Exception ex)
            {
                systemLog.Error(LOG_PREFIX + " Could not load transcriber extension", ex);
            }

            if (transcriber != null)
            {
                systemLog.Debug(LOG_PREFIX + " Start transcribing");

                transcriber.TextAvailable += HandleText;
                transcriber.Start();
                
                State     = SessionState.Active;
                StartTime = DateTime.UtcNow;

                sessionLog.Info($"{ LOG_PREFIX } Session { Id } started");
            }
            
            if (Record)
            {
                StartRecording();
            }

            if (Consumer != null)
            {
                Consumer.DataAvailable += HandleData;
                Consumer.Start();
            }

            using (var entities = new Entities())
            {
                entities.Entry(this).State = EntityState.Modified;
                await entities.SaveChangesAsync();
            }
        }

        public async void End()
        {
            StopRecording();

            if (provider != null)
            {
                systemLog.Debug(LOG_PREFIX + " Stop provider");
                provider.Stop();
            }
            
            // Stop consuming
            if (Consumer != null)
            {
                systemLog.Debug(LOG_PREFIX + " Stop recording");
                Consumer.DataAvailable -= HandleData;
                Consumer.Stop();
            }

            // Stop transcribing
            if (transcriber != null)
            {
                systemLog.Debug(LOG_PREFIX + " End transcribing");
                transcriber.TextAvailable -= HandleText;
                transcriber.End();
            }

            EndTime = DateTime.UtcNow;
            State   = SessionState.Finished;

            using (var entities = new Entities())
            {
                      entities.Sessions.Attach(this);
                      // Do NOT update word count; because the Archive API does this
                      entities.Entry(this).Property(p => p.WordCount).IsModified = false;
                await entities.SaveChangesAsync();
            }

            // Write to session log
            sessionLog.Info(LOG_PREFIX + " Session stopped");
        }

        public bool StartRecording()
        {
            try
            {
                recorder = Shared.Extensions.Instance.Recorders
                    .FirstOrDefault(t => t.Metadata.Name.Equals("Wave") && t.Metadata.Version.Equals("1.0"))?
                        .Value;
            }
            catch (Exception ex)
            {
                systemLog.Error(LOG_PREFIX + " Could not load transcriber extension", ex);
            }

            if (recorder != null)
            {
                systemLog.Debug(LOG_PREFIX + " Start recording");

                recorder.Start(Id);
                recording = true;
            }

            return recording;
        }

        public void StopRecording()
        {
            if (recording)
            {
                recording = false;

                if (recorder != null)
                {
                    recorder.Stop();
                    recorder = null;
                }
            }
        }

        private void Create(Entities entities)
        {
            // TODO Replace with messaging back to Controller
            EncoderId = Encoder.Id;
            StationId = Station.Id;

            entities.Sessions.Add(this);

            // Must run synchronous for now; because Session.Id must be set
            entities.SaveChanges();
        }
        
        private void HandleData(object sender, DataEventArgs e)
        {
            if (recorder != null && recording)
            {
                try
                {
                    recorder.Record(e.Buffer, e.Length);
                }
                catch (Exception ex)
                {
                    systemLog.Error($"{LOG_PREFIX} Error while trying to send audio to recorder");
                    systemLog.Debug(ex.Message);
                    systemLog.Debug(ex.StackTrace);
                }
            }

            if (transcriber != null && transcriber.IsReady)
            {
                try
                {
                    transcriber.Transcribe(e.Buffer, e.Length);
                }
                catch (Exception ex)
                {
                    systemLog.Error($"{LOG_PREFIX} Error while trying to send audio to transcriber");
                    systemLog.Debug(ex.Message);
                    systemLog.Debug(ex.StackTrace);
                }
            }
        }

        private void HandleText(object sender, TextEventArgs e)
        {
            // Convert to the required encoding (e.g. CEA-608)
            foreach (var bytes in converter.Convert(e.Text, e.Encoding))
            {
                // Word counter and text logger
                if (archive != null)
                {
                    try
                    {
                        archive.Send(Encoding.Convert(e.Encoding, Encoding.UTF8, bytes));
                    }
                    catch (Exception ex)
                    {
                        systemLog.Error($"{LOG_PREFIX} Error while trying to send captions to archive");
                        systemLog.Debug(ex.Message);
                        systemLog.Debug(ex.StackTrace);
                    }
                }

                // Provide to Caption Encoder
                if (provider != null)
                {
                    systemLog.DebugFormat("{0} Sending transcription to provider: {1}", LOG_PREFIX, e.Encoding.GetString(bytes));

                    try
                    { 
                        provider.Send(bytes);
                    }
                    catch (Exception ex)
                    {
                        systemLog.Error(LOG_PREFIX + " Error while trying to send captions to provider", ex);
                    }
                }
            }
        }
    }

    public class SessionMetadata
    {
        [JsonIgnore]
        public virtual Station Station { get; set; }
        [JsonIgnore]
        public virtual Encoder Encoder { get; set; }
        [JsonIgnore]
        public virtual Server  Server  { get; set; }
        [JsonIgnore]
        public virtual Rate    Rate    { get; set; }
    }
}
