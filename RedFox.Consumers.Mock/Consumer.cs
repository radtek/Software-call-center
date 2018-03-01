using RedFox.Shared;

using System.ComponentModel.Composition;

namespace RedFox.Consumers.Mock
{
    [Export(typeof(IConsumer)), ConsumerMetadata("Mock", "1.0", Direction.Out)]
    public class Consumer : IConsumer
    {
        public State State     { get; private set; }
        public int   SessionId { get; set; }

        public event DataHandler           DataAvailable;
        public event ConsumerStateDelegate StateChanged;

        public void Init()
        {
            
        }

        public void Finish()
        {

        }

        public void Start(string endpoint)
        {
            
        }

        public void Stop()
        {
            
        }

        public void Pause()
        {

        }

        public void Resume()
        {

        }
    }
}
