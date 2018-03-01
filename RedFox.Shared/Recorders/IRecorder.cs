namespace RedFox.Recorders
{
    public interface IRecorder
    {
        void Start(int sessionId);
        void Record(byte[] data, int count);
        void Stop();
    }
}
