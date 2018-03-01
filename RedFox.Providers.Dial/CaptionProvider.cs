using log4net;

using RedFox.Providers;
using RedFox.Shared.Interfaces;

using System.ComponentModel.Composition;
using System.Configuration;
using System.IO.Ports;
using System.Threading.Tasks;

namespace RedFox.Provider.Dial
{
    [Export(typeof(ICaptionProvider)), ExtensionMetadata("Dial-out", "1.0")]
    public class CaptionProvider : ICaptionProvider
    {
        private enum State
        {
            Closed, Open, Available, Calling, Busy, Answered, Online, Hangup
        }

        private static ILog   systemLog = LogManager.GetLogger("System");
        private static string prefix    = "ICaptionProvider [Dial-out 1.0]:";

        public string Endpoint  { get; set; }
        public string Settings  { get; set; }
        public int    SessionId { private get; set; }

        private SerialPort port;
        private bool       muted;
        private State      state = State.Closed;

        public CaptionProvider()
        {
            var com   = "COM4";

            // Fetch list of defined COM ports
            var ports = ConfigurationManager.AppSettings["RedFox.Providers.Dial.Ports"].Split(',');

            // Check which port is available
            foreach (var name in ports)
            {
                
            }

            systemLog.DebugFormat("{0} Init serial port on COM4", prefix);
            
            // Set baud rate etc
            port = new SerialPort(com, 9600, Parity.None, 8, StopBits.One)
            {
                Handshake    = Handshake.RequestToSend,
                ReadTimeout  = 500,
                WriteTimeout = 500,
                DtrEnable    = true
            };

            systemLog.DebugFormat("{0} COM port ready", prefix);

            port.DataReceived += Port_DataReceived;
        }

        public void Init()
        {
            Task.Run(() => { Go(); });
        }

        private void Go()
        {
            try
            {
                systemLog.DebugFormat("{0} Open COM4", prefix);

                port.Open();

                state = State.Open;
            }
            catch
            {
                systemLog.DebugFormat("{0} Cannot open COM4", prefix);
                return;
            }

            systemLog.DebugFormat("{0} AT", prefix);

            // AT to modem
            port.Write("AT");
            port.Write(new byte[] { 13 }, 0, 1);
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = port.ReadExisting();
            
            systemLog.DebugFormat("{0} Response: {1}", prefix, data);
            
            switch (state)
            {
                case State.Open:
                    if (!data.Contains("OK"))
                    {
                        systemLog.DebugFormat("{0} No Response", prefix);

                        Stop();
                        return;
                    };

                    state = State.Available;

                    //systemLog.DebugFormat("{0} ATE", prefix);

                    //port.Write("ATE");
                    //port.Write(new byte[] { 13 }, 0, 1);

                    systemLog.DebugFormat("{0} ATDT {1}", prefix, Endpoint);

                    port.Write("ATDT " + Endpoint);
                    port.Write(new byte[] { 13 }, 0, 1);

                    state = State.Calling;
                    break;

                case State.Calling:
                    if (!data.Contains("CONNECT"))
                    {
                        return;
                    }

                    state = State.Answered;

                    systemLog.DebugFormat("{0} Connected", prefix);
                    
                    port.Write(new byte[] { 1, 79, 13 }, 0, 3);
                    System.Threading.Thread.Sleep(1000);

                    systemLog.DebugFormat("{0} Caption encoder port activated", prefix);
                    
                    port.Write(new byte[] { 1, 63, 13 }, 0, 3);
                    System.Threading.Thread.Sleep(1000);

                    systemLog.DebugFormat("{0} Caption encoder information request", prefix);

                    port.Write(new byte[] { 1, 50, 13 }, 0, 3);
                    System.Threading.Thread.Sleep(1000);

                    systemLog.DebugFormat("{0} Caption encoder in RealTime mode", prefix);

                    state = State.Online;
                    break;
            }
        }

        public void Send(byte[] data)
        {
            if (muted || state != State.Online) return;

            port.Write(data, 0, data.Length);
            port.Write(new byte[] { 13 }, 0, 1);
        }

        public void Stop()
        {
            // ATH 
            if (port != null && port.IsOpen)
            {
                state = State.Hangup;

                systemLog.DebugFormat("{0} ATH", prefix);

                port.Write("ATH");
                port.Write(new byte[] { 13 }, 0, 1);
                
                port.Close();
                port.Dispose();

                state = State.Closed;
            }
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
