using Google.Apis.Auth.OAuth2;
using Google.Cloud.Speech.V1;

using Grpc.Auth;
using Grpc.Core;

using log4net;

using RedFox.Shared.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedFox.Transcribers.GoogleCloudSpeech
{
    [Export(typeof(ITranscriber)), ExtensionMetadata("Google Cloud Speech", "1.0")]
    public class Transcriber : ITranscriber
    {
        private static ILog   systemLog = LogManager.GetLogger("System");
        private static string prefix    = "ITranscriber [Google Cloud Speech 1.0]:";

        public bool IsReady { get; private set; }
        
        public event TextHandler TextAvailable;

        private Task         handle;
        private SpeechClient speech;
        private SpeechStream stream;
        private Timer        timer;
        private AudioState   state;
        private DateTime     timestamp;
        private int          treshold = 1000;

        private SpeechRecognitionEngine               engine;
        private SpeechClient.StreamingRecognizeStream streaming;
        
        public async void Start()
        {
            try
            {
                var credentials = GoogleCredential.FromFile("Extensions/25670f9058d6.json");
                var channel     = new Channel(SpeechClient.DefaultEndpoint.Host, credentials.ToChannelCredentials());

                timer     = new Timer(Elapsed, null, 60000, Timeout.Infinite);
                speech    = SpeechClient.Create(channel);
                stream    = new SpeechStream(65536);
                streaming = speech.StreamingRecognize();
                engine    = new SpeechRecognitionEngine();

                engine.LoadGrammar(new DictationGrammar());
                engine.AudioStateChanged += Engine_AudioStateChanged;
                engine.SetInputToAudioStream(stream, new SpeechAudioFormatInfo(EncodingFormat.ULaw, 8000, 8, 1, 8000, 1, null));
                engine.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception ex)
            {
                systemLog.Error(prefix + " Could not create SpeechClient", ex);
            }
            
            await streaming.WriteAsync(new StreamingRecognizeRequest()
            {
                StreamingConfig = new StreamingRecognitionConfig()
                {
                    InterimResults = true,
                    Config         = new RecognitionConfig()
                    {
                        Encoding        = RecognitionConfig.Types.AudioEncoding.Mulaw,
                        SampleRateHertz = 8000,
                        LanguageCode    = "en-US"
                    },
                }
            });

            systemLog.DebugFormat("{0} Google Speech To Text stream started", prefix);

            IsReady = true;
            handle  = Task.Run(async() => 
            {
                while (await streaming.ResponseStream.MoveNext(default(CancellationToken)))
                {
                    try
                    {
                        foreach (var result in streaming.ResponseStream.Current.Results)
                        {
                            if (!result.IsFinal) continue;

                            if (result.Alternatives.Count == 0) continue;

                            var alternative = result.Alternatives.OrderByDescending(a => a.Confidence).FirstOrDefault();
                            var args        = new TextEventArgs(alternative.Transcript, Encoding.UTF8);

                            TextAvailable(this, args);
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            }); 
        }
        
        private void Engine_AudioStateChanged(object sender, AudioStateChangedEventArgs e)
        {
            state = e.AudioState;

            if (state == AudioState.Silence)
            {
                timestamp = DateTime.Now;
            }
        }

        public async void End()
        {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            stream.Close();

            IsReady = false;

            systemLog.DebugFormat("{0} Google Speech To Text stream ended", prefix);

            await streaming.WriteCompleteAsync();
            await handle;

            Task.Run(() => engine.RecognizeAsyncCancel());
        }
        
        public void Transcribe(byte[] buffer, int length)
        {
            if (state == AudioState.Silence)
            {
                var silence = DateTime.Now - timestamp;

                if (silence.TotalMilliseconds > treshold)
                {
                    systemLog.DebugFormat("{0} Silence detected", prefix);
                    Elapsed(null);
                }
            }

            var tasks = new List<Task>
            {
                   stream.WriteAsync(buffer, 0, length),
                streaming.WriteAsync(new StreamingRecognizeRequest() { AudioContent = Google.Protobuf.ByteString.CopyFrom(buffer, 0, length) })
            };

            Task.WaitAll(tasks.ToArray());
        }

        private void Elapsed(object state)
        {
            End(); Start();
        }
    }
}
