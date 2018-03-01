using log4net;

using RedFox.Shared.Interfaces;

using System.ComponentModel.Composition;
using System.Net.Sockets;

namespace RedFox.Providers.IP
{
    [Export(typeof(ICaptionProvider)), ExtensionMetadata("Telnet", "1.0")]
    public class TelnetProvider : ICaptionProvider
    {
        private static ILog   logger = LogManager.GetLogger("System");
        private static string prefix = "ICaptionProvider [Telnet 1.0]:";

        private TcpClient client;
        private bool      muted; 

        public string Endpoint  { get; set; }
        public string Settings  { get; set; }
        public int    SessionId { private get; set; }

        public async void Init()
        {
            // Open telnet session
            var address = Endpoint.Split(':')[0];
            var port = int.Parse(Endpoint.Split(':')[1]);

            client = new TcpClient
            {
                ExclusiveAddressUse = true
            };

            try
            { 
                await client.ConnectAsync(address, port);
            }
            catch (SocketException ex)
            {
                logger.Error(prefix + " Error connecting to " + address + ":" + port, ex);
            }

            if (client.Connected)
            {
                logger.DebugFormat("{0} Provider Telnet is connected to {1}", prefix, Endpoint);

                // Send Ctrl-A and 2 to start RealTime Mode
                var output = new byte[] { 1, 50, 13 };

                client.GetStream().Write(output, 0, output.Length);

                logger.DebugFormat("{0} Caption encoder in RealTime mode", prefix);
            }
        }

        public void Send(byte[] data)
        {
            if (muted || client == null || !client.Connected) return;

            client.GetStream().Write(data, 0, data.Length);
            client.GetStream().Write(new byte[] { 13 } , 0, 1);
        }

        public void Stop()
        {
            if (client == null) return;

            if (client.Connected)
            {
                client.Close();
                logger.DebugFormat("{0} Telnet connection to {1} closed", prefix, Endpoint);
            }

            client.Dispose();
        }

        public void Mute()
        {
            muted = true;
        }

        public void Unmute()
        {
            muted = false;
        }
    }
}
