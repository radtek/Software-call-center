using IBM.WatsonDeveloperCloud.SpeechToText.v1.Model;

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RedFox.Shared.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;

using WebSocketSharp;

namespace RedFox.Transcribers.Watson
{
    [Export(typeof(ITranscriber)), ExtensionMetadata("Watson", "2.0")]
    public class TranscriberWs : ITranscriber
    {
        private static ILog   systemLog = LogManager.GetLogger("System");
        private static string prefix    = "ITranscriber [Watson 2.0]";

        private WebSocket   webSocket;
        private DateTime    timer;
        private int         respite   = 000;
        private int         position  = 0;
        private List<int>   positions = new List<int>(4) { 0 };

        public event TextHandler TextAvailable;

        public bool IsReady { get; private set; }

        public void Transcribe(byte[] data, int length)
        {
            if (webSocket.ReadyState == WebSocketState.Open)
            {
                webSocket.Send(data);
            }
        }

        public void Start()
        {
            String model = "en-US_NarrowbandModel";
            /*
            Supported models:
                en-GB_NarrowbandModel
                en-US_NarrowbandModel
                es-ES_NarrowbandModel
                ja-JP_NarrowbandModel
                pt-BR_NarrowbandModel
                zh-CN_NarrowbandModel
             */

            webSocket = new WebSocket("wss://stream.watsonplatform.net/speech-to-text/api/v1/recognize?model=" + model);

            webSocket.OnMessage += Ws_OnMessage;
            webSocket.OnClose   += Ws_OnClose;
            webSocket.OnError   += Ws_OnError;
            webSocket.OnOpen    += Ws_OnOpen;

            webSocket.SetCredentials("cf674729-a53a-4f57-93cb-f63ecb7b59e2", "xjG8zUDio1I4", true);
            webSocket.ConnectAsync();
        }

        private void Ws_OnOpen(object sender, EventArgs e)
        {
            systemLog.DebugFormat("{0} Connection to Watson open", prefix);

            dynamic parameters = new JObject();
            
            parameters.content_type       = "audio/mulaw;rate=8000";
            parameters.inactivity_timeout = -1;
            parameters.profanity_filter   = true;
            parameters.interim_results    = true;
            parameters.timestamps         = true;
            parameters.smart_formatting   = true;
            parameters.speaker_labels     = false;

            var json = JsonConvert.SerializeObject(parameters);

            ((WebSocket)sender).Send(json);

            //((WebSocket)sender).Send("{\"action\": \"start\", \"content-type\": \"audio/mulaw;rate=8000\", \"profanity_filter\": true, \"interim_results\": true, \"timestamps\": true, \"smart_formatting\": true, \"speaker_labels\": false }");

            systemLog.DebugFormat("{0} Watson instructed to start listening for {1}", prefix, parameters["content_type"]);
        }

        private void Ws_OnError(object sender, ErrorEventArgs e)
        {
            systemLog.Error($"{prefix} {e.Message}");
            systemLog.Debug(e.Exception.Message);
            systemLog.Debug(e.Exception.StackTrace);
        }

        private void Ws_OnClose(object sender, CloseEventArgs e)
        {
            systemLog.DebugFormat("{0} Connection to Watson closed", prefix);
        }

        private void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            systemLog.DebugFormat("{0} Incoming message from Watson", prefix);

            var json = e.Data;
            var obj  = JsonConvert.DeserializeObject<SpeechRecognitionEvent>(json);
            
            if (obj.State == "listening")
            {
                IsReady = true;
                timer   = DateTime.Now;

                systemLog.DebugFormat("{0} Watson listening", prefix);
            }
            
            if (!obj.Error.IsNullOrEmpty())
            {
                IsReady = false;

                systemLog.ErrorFormat("{0} {1}", prefix, obj.Error);
            }

            if (obj.Results == null)
            {
                return;
            }
            
            var elapsed = DateTime.Now.Subtract(timer).TotalMilliseconds;
            var final   = obj.Results.Find(t => t.Final);

            if (elapsed > respite || final != null)
            {
                foreach (var result in obj.Results)
                {
                    if (result.Alternatives.Count == 0) continue;

                    var interim = result.Alternatives[0].Transcript;

                    systemLog.Debug(interim);

                    if (interim.Length <= position)
                    {
                        positions.Clear();
                        position = 0;
                        timer    = DateTime.Now;

                        continue;
                    }

                    if (final == null)
                    {
                        for (var index = position; index < interim.Length; index++)
                        {
                            if (char.IsWhiteSpace(interim[index]))
                            {
                                if (positions.Count == 4)
                                    positions.RemoveAt(0);

                                positions.Add(index + 1);
                            }
                        }
                        
                        TextAvailable?.Invoke(this, new TextEventArgs(interim.Substring(position, positions[0] - position), Encoding.UTF8));

                        position = positions[0];
                    }
                    else
                    {
                        TextAvailable?.Invoke(this, new TextEventArgs(interim.Substring(position, interim.Length - position), Encoding.UTF8));

                        //positions.Clear();
                        //position = 0;
                        //timer    = DateTime.Now;
                    }
                }
            }
        }
        
        public void End()
        {
            if (webSocket.ReadyState == WebSocketState.Open)
            {
                systemLog.DebugFormat("{0} Telling Watson to stop listening", prefix);
                webSocket.Send("{\"action\": \"stop\"}");

                systemLog.DebugFormat("{0} Closing websocket connection", prefix);
                webSocket.CloseAsync(CloseStatusCode.Normal);
            }
        }
    }
}