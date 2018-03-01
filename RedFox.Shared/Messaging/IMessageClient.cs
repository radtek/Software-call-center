using RedFox.Shared.Interfaces;

using System;

namespace RedFox.Messaging
{
    public delegate void MessageDelegate(object sender, MessageEventArgs e);

    public class MessageEventArgs : EventArgs
    {
        public string Sender  { get; private set; }
        public string Message { get; private set; }

        public MessageEventArgs(string sender, string message)
        {
            Sender  = sender;
            Message = message;
        }
    }

    public interface IMessageClient : IRedFox
    {
        event MessageDelegate MessageReceived;

        void Init(string identity);
        void Send(string recipient, string message);
        void Listen();
        void Shutdown();
    }
}
