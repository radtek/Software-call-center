using NAudio.Wave;

using RedFox.Shared;

using System.ComponentModel.Composition;

namespace RedFox.Consumers.WavRecorder
{
    [Export(typeof(IConsumer)), ConsumerMetadata("AudioFile", "1.0", Direction.Out)]
    public class AudioFile : IConsumer
    {  
        public State State     { get; private set; }
        public int   SessionId { get; set; }

        public event DataHandler           DataAvailable;
        public event ConsumerStateDelegate StateChanged;

        private WaveFormat format = new WaveFormat(16000, 16, 1);

        public void Init()
        {
            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Ready, "", SessionId));
        }

        public void Finish()
        {
            
        }

        public void Start(string endpoint)
        {
            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Busy, "", SessionId));

            byte[] buffer = null;

            using (var file = new Mp3FileReader(@"C:\Users\karel\Downloads\102-Keith-Million.mp3"))
            {
                using (var stream = WaveFormatConversionStream.CreatePcmStream(file))
                {
                    using (var raw = new RawSourceWaveStream(stream, format))
                    {
                        buffer = new byte[raw.Length];

                        raw.Read(buffer, 0, buffer.Length);
                    }
                }
            }

            if (buffer != null && buffer.Length > 0)
                DataAvailable?.Invoke(this, new DataEventArgs(buffer, buffer.Length));

            StateChanged?.Invoke(this, new ConsumerStateEventArgs(State.Finished, "", SessionId));
        }

        public void Pause()
        {
            
        }

        public void Resume()
        {
            
        }

        public void Stop()
        {
            
        }
    }
}
