using RedFox.Shared.Interfaces;

using System.ComponentModel.Composition;

namespace RedFox.Providers.Mock
{
    [Export(typeof(ICaptionProvider)), ExtensionMetadata("Mock", "1.0")]
    public class CaptionProvider : ICaptionProvider
    {
        public string Endpoint  { get; set; }
        public string Settings  { get; set; }
        public int    SessionId { private get; set; }

        private bool muted;

        public void Init()
        {
            
        }

        public void Send(byte[] data)
        {
        }

        public void Stop()
        {

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
