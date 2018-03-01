using RedFox.Shared.Interfaces;

using System;
using System.Text;

namespace RedFox.Transcribers
{
    public delegate void TextHandler(object sender, TextEventArgs e);

    public class TextEventArgs : EventArgs
    {
        public string   Text     { get; private set; }
        public Encoding Encoding { get; private set; }

        public TextEventArgs(string text, Encoding encoding)
        {
            Text     = text;
            Encoding = encoding;
        }
    }

    public interface ITranscriber : IRedFox
    {
        event TextHandler TextAvailable;

        void Transcribe(byte[] buffer, int length);
        void Start();
        void End();

        bool IsReady { get; }
    }
}
