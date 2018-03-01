using RedFox.Shared.Interfaces;

namespace RedFox.Providers
{
    public interface ICaptionProvider : IRedFox
    {
        string Endpoint  { get; set; }
        string Settings  { get; set; }

        int SessionId { set; }

        void Init();
        void Send(byte[] data);
        void Stop();
        void Mute();
        void Unmute();
    }
}
