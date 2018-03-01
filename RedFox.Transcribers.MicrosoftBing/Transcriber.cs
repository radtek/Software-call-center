using log4net;

using Microsoft.CognitiveServices.SpeechRecognition;

using NAudio.Codecs;
using NAudio.Wave;
using RedFox.Shared.Interfaces;

using System.ComponentModel.Composition;
using System.IO;

namespace RedFox.Transcribers.MicrosoftBing
{
    [Export(typeof(ITranscriber)), ExtensionMetadata("Microsoft Bing", "1.0")]
    public class Transcriber : ITranscriber
    {
        private static ILog   systemLog = LogManager.GetLogger("System");
        private static string prefix    = "ITranscriber [Microsoft Bing 1.0]";
        private static string key1      = "0dd92d66808e415e8b4309fee93b11c3";
        private static string key2      = "235e059b37ae47b2aee05e9fd5ce9702";

        private DataRecognitionClient client;

        public bool IsReady { get; private set; }

        public event TextHandler TextAvailable;
        
        public void Start()
        {
            var location  = System.Reflection.Assembly.GetEntryAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            var filename  = "Recordings/linear.wav";
            var path      = string.Format(@"{0}/{1}", directory, filename);
            var format    = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, 8000, 1, 16000, 2, 16);

            client = SpeechRecognitionServiceFactory.CreateDataClient(SpeechRecognitionMode.LongDictation, "en-US", key1);
            
            client.OnConversationError       += Client_OnConversationError;
            client.OnIntent                  += Client_OnIntent;
            client.OnMicrophoneStatus        += Client_OnMicrophoneStatus;
            client.OnPartialResponseReceived += Client_OnPartialResponseReceived;
            client.OnResponseReceived        += Client_OnResponseReceived;
            
            client.AudioStart();
            client.SendAudioFormat(new SpeechAudioFormat
            {
                AverageBytesPerSecond = 16000,
                BitsPerSample         = 16,
                BlockAlign            = 2,
                ChannelCount          = 1,
                EncodingFormat        = AudioCompressionType.PCM,
                SamplesPerSecond      = 8000
            });
            
            IsReady = true;
        }
        
        public void End()
        {
            IsReady = false;
            
            client.AudioStop();
        }

        public void Transcribe(byte[] buffer, int length)
        {
            //var mulaw = WaveFormat.CreateCustomFormat(WaveFormatEncoding.MuLaw, 8000, 1, 8000, 1, 8);
            //var pcm   = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, 8000, 1, 16000, 2, 16);

            //using (var raw = new RawSourceWaveStream(buffer, 0, length, mulaw))
            //{
            //    using (var stream = new WaveFormatConversionStream(pcm, raw))
            //    {
            //        var output = new byte[length * 2];
            //        var read   = stream.Read(output, 0, length * 2);

            //        client.SendAudio(output, read);
            //    }
            //}

            var output = new byte[length * 2];
            var index  = 0;

            for (var b = 0; b < length; b++)
            {
                var sample = MuLawDecoder.MuLawToLinearSample(buffer[b]);

                output[index]     = (byte)(sample & 0xFF);
                output[index + 1] = (byte)(sample >> 8);
            }
            
            client.SendAudio(output, output.Length);
        }
        
        private void Client_OnResponseReceived(object sender, SpeechResponseEventArgs e)
        {
            systemLog.Debug($"{ prefix } Response received ({ e.PhraseResponse.RecognitionStatus })");

            for (var i = 0; i < e.PhraseResponse.Results.Length; i++)
            {
                systemLog.Debug($"{ i + 1 } ({ e.PhraseResponse.Results[i].Confidence }): {  e.PhraseResponse.Results[i].DisplayText }");
            }
        }

        private void Client_OnPartialResponseReceived(object sender, PartialSpeechResponseEventArgs e)
        {
            systemLog.Debug($"{ prefix } Partial response received");
            systemLog.Debug(e.PartialResult);
        }

        private void Client_OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            systemLog.Debug($"{ prefix } Microphone status changed (recording: { e.Recording })");
        }

        private void Client_OnIntent(object sender, SpeechIntentEventArgs e)
        {
            systemLog.Debug($"{ prefix } Intent ...");
        }

        private void Client_OnConversationError(object sender, SpeechErrorEventArgs e)
        {
            systemLog.Debug($"{ prefix } Conversation error { e.SpeechErrorCode }: { e.SpeechErrorText }");
        }
    }
}