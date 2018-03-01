using NAudio.Wave;
using RedFox.Shared;

using System.ComponentModel.Composition;

namespace RedFox.Consumers.Loopback
{
    [Export(typeof(IConsumer)), ConsumerMetadata("Loopback", "1.0", Direction.Out)]
    public class Consumer : IConsumer
    {
        public State State     { get; private set; }
        public int   SessionId { get; set; }

        private IWaveIn     waveIn;
        private MuLawStream stream;

        public event DataHandler           DataAvailable;
        public event ConsumerStateDelegate StateChanged;

        private bool paused;

        public void Init()
        {
            waveIn = new WasapiLoopbackCapture();
            stream = new MuLawStream();

            waveIn.RecordingStopped += WaveIn_RecordingStopped;
            waveIn.DataAvailable    += stream.OnDataAvailable;
            stream.DataAvailable    += Stream_DataAvailable;

            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Listening, "+4747285576", SessionId));
        }

        public void Start(string endpoint)
        {
            waveIn.StartRecording();

            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Busy, "", SessionId));
        }

        public void Finish()
        {
            waveIn.StopRecording();
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }
        
        public void Stop()
        {
            waveIn.StopRecording();
        }
        
        private void Stream_DataAvailable(object sender, DataEventArgs e)
        {
            DataAvailable?.Invoke(this, e);
        }

        private void WaveIn_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (waveIn != null)
                waveIn.Dispose();
        }
    }
}
