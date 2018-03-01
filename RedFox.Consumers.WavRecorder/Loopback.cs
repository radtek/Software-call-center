using NAudio.CoreAudioApi;
using NAudio.Wave;

using RedFox.Shared;

using System.ComponentModel.Composition;

namespace RedFox.Consumers.WavRecorder
{
    [Export(typeof(IConsumer)), ConsumerMetadata("Obsolete", "1.0", Direction.Out)]
    public class Loopback : IConsumer
    {
        public event DataHandler           DataAvailable;
        public event ConsumerStateDelegate StateChanged;
        
        private bool paused;

        private WasapiLoopbackCapture capture;

        public State State     { get; private set; }
        public int   SessionId { get; set; }
        
        public void Init()
        {
            capture = new WasapiLoopbackCapture
            {
                ShareMode = AudioClientShareMode.Exclusive
            };

            capture.DataAvailable    += Capture_DataAvailable;
            capture.RecordingStopped += Capture_RecordingStopped;
            capture.StartRecording();
                        
            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Listening, "+4747285576", SessionId));
        }

        public void Finish()
        {

        }

        public void Start(string endpoint)
        {
            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Busy, "", SessionId));
        }

        public void Stop()
        {
            if (capture != null)
                capture.StopRecording();
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }

        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            var buffer = new WaveBuffer(e.Buffer);
            var array  = new byte[e.BytesRecorded / 24];

            for (int i = 0; i < e.BytesRecorded; i += 24)
            {
                var sample = i / 24;
                var left   = buffer.FloatBuffer[sample];
                var right  = buffer.FloatBuffer[sample + 1];
                
            }
        }

        private void Capture_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (capture != null)
                capture.Dispose();

            capture = null;
        }
        
    }
}
