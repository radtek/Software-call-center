using RedFox.Shared;
using RedFox.Shared.Interfaces;

using System;

namespace RedFox.Consumers
{
    public delegate void DataHandler(object sender, DataEventArgs e);
    public delegate void ConsumerStateDelegate(object sender, ConsumerStateEventArgs e);
    
    public class DataEventArgs : EventArgs
    {
        public byte[] Buffer { get; private set; }
        public int    Length { get; private set; }

        public DataEventArgs(byte[] buffer, int length)
        {
            Buffer = buffer;
            Length = length;
        }
    }

    public class ConsumerStateEventArgs : EventArgs
    {
        public State  NewState  { get; private set; }
        public string Endpoint  { get; private set; }
        public int    SessionId { get; private set; }

        public ConsumerStateEventArgs(State newState, string endpoint, int sessionId)
        {
            NewState  = newState;
            Endpoint  = endpoint;
            SessionId = sessionId;
        }
    }

    public interface IConsumer : IRedFox
    {
        event DataHandler           DataAvailable;
        event ConsumerStateDelegate StateChanged;

        State State     { get; }
        int   SessionId { get; set; }

        void Init();
        void Finish();

        void Start(string endpoint);
        void Stop();

        void Pause();
        void Resume();
    }
}
