using RedFox.Shared;

using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace RedFox.Consumers.WebSocket
{
    [Export(typeof(IConsumer)), ConsumerMetadata("WebSocket", "1.0", Direction.In)]
    public class Consumer : IConsumer
    {
        public State State     { get; private set; }
        public int   SessionId { get; set; }

        public event DataHandler           DataAvailable;
        public event ConsumerStateDelegate StateChanged;

        private TcpListener server;
        private bool        paused;
        
        public void Init()
        {
            server = new TcpListener(IPAddress.Any, 8140);
            server.Start();

            Task.Run(() => {
                ThreadPool.QueueUserWorkItem(Accept, server.AcceptTcpClient());
            });
        }

        public void Finish()
        {

        }

        public void Start(string endpoint)
        {
            
        }

        public void Stop()
        {
            server.Stop();
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            paused = false;
        }

        private void Accept(object obj)
        {
            var client = (TcpClient) obj;
            var stream = client.GetStream();
            var bytes  = new byte[client.ReceiveBufferSize];

            stream.Read(bytes, 0, bytes.Length);

            // TODO Some header required

            if (paused) return;

            DataAvailable?.Invoke(this, new DataEventArgs(bytes, bytes.Length));
        }
    }
}
