using log4net;

using RedFox.Shared.Interfaces;

using System;
using System.ComponentModel.Composition;
using System.Speech.AudioFormat;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Transcribers.MicrosoftSpeech
{
    [Export(typeof(ITranscriber)), ExtensionMetadata("Microsoft Speech", "1.0")]
    public class Transcriber : ITranscriber
    {
        private static ILog   systemLog = LogManager.GetLogger("System");
        private static string prefix    = "ITranscriber [Microsoft Speech 1.0]:";

        private SpeechRecognitionEngine speechRecognitionEngine = null;

        private SpeechStream   stream;

        public bool IsReady { get; private set; }
        
        public event TextHandler TextAvailable;

        public void Start()
        {
            

            foreach (RecognizerInfo config in SpeechRecognitionEngine.InstalledRecognizers())
            {
                if (config.Culture.ToString() == "en-US")
                {
                    speechRecognitionEngine = new SpeechRecognitionEngine(config);
                    break;
                }
            }

            if (speechRecognitionEngine == null)
            {

            }

            stream = new SpeechStream(65536);

            //var choices = new Choices();
            //choices.Add(new string[] { "hello", "test", "one", "two", "three" });

            //var builder = new GrammarBuilder();
            //builder.Append(choices);

            speechRecognitionEngine.InitialSilenceTimeout = new TimeSpan(0, 0, 1);
            speechRecognitionEngine.LoadGrammar(new DictationGrammar());
            //speechRecognitionEngine.LoadGrammar(new Grammar(builder));
            //speechRecognitionEngine.AudioLevelUpdated          += (s, e) => { systemLog.DebugFormat("{0} Audio level updated to {1}", prefix, e.AudioLevel); };
            //speechRecognitionEngine.AudioSignalProblemOccurred += (s, e) => { systemLog.DebugFormat("{0} Audio signal problem occurred; {1}", prefix, e.AudioSignalProblem.ToString());  };
            //speechRecognitionEngine.AudioStateChanged          += (s, e) => { systemLog.DebugFormat("{0} Audio state changed to {1}", prefix, e.AudioState.ToString()); };
            speechRecognitionEngine.RecognizeCompleted         += SpeechRecognitionEngine_RecognizeCompleted;
            speechRecognitionEngine.SpeechRecognized           += SpeechRecognitionEngine_SpeechRecognized;
            //speechRecognitionEngine.SpeechDetected             += (s, e) => { systemLog.DebugFormat("{0} Speech detected", prefix); };
            //speechRecognitionEngine.SpeechRecognitionRejected  += (s, e) => { systemLog.DebugFormat("{0} Speech recognition rejected", prefix); };
            //speechRecognitionEngine.SpeechHypothesized         += (s, e) => { systemLog.DebugFormat("{0} Speech hypothesized", prefix); };
            
            var safi = new SpeechAudioFormatInfo(EncodingFormat.ULaw, 8000, 8, 1, 8000, 1, null);
            // new SpeechAudioFormatInfo(8000, AudioBitsPerSample.Sixteen, AudioChannel.Mono);

            speechRecognitionEngine.SetInputToAudioStream(stream, safi);
            speechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple);
            
            IsReady = true;
        }

        public void End()
        {

            stream.Close();

            Task.Run(() => speechRecognitionEngine.RecognizeAsyncCancel());
        }

        public async void Transcribe(byte[] buffer, int length)
        {
            await stream.WriteAsync(buffer, 0, length);
        }

        private void SpeechRecognitionEngine_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            TextAvailable?.Invoke(this, new TextEventArgs(e.Result.Text, Encoding.UTF8));
        }
        
        private void SpeechRecognitionEngine_RecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            systemLog.DebugFormat("{0} Recognize completed", prefix);
            //systemLog.DebugFormat("  Babble timeout: {0}", e.BabbleTimeout);
            //systemLog.DebugFormat("  Cancelled: {0}", e.Cancelled);
            //systemLog.DebugFormat("  Error: {0}", e.Error?.Message);
            //systemLog.DebugFormat("  Initial Silence Timeout: {0}", e.InitialSilenceTimeout);
            //systemLog.DebugFormat("  Input Stream Ended: {0}", e.InputStreamEnded);

            if (e.Error != null) systemLog.Error(prefix + " " + e.Error.Message, e.Error);

            stream = null;
        }
    }
}
