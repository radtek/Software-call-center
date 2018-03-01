using RedFox.Shared.Interfaces;

using System.ComponentModel.Composition;
using System.IO;
using System.Text;

namespace RedFox.Transcribers.Mock
{
    [Export(typeof(ITranscriber)), ExtensionMetadata("Mock", "1.0")]
    public class Transcriber : ITranscriber
    {
        public event TextHandler TextAvailable;
        
        public bool IsReady { get; private set; }

        public void Start()
        {
            IsReady = true;

            using (var reader = new StreamReader(@"C:\Users\karel\Downloads\sample.txt"))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        TextAvailable?.Invoke(this, new TextEventArgs(line, Encoding.UTF8));
                    }
                }

                reader.Close();
            }
        }

        public void Transcribe(byte[] buffer, int length)
        {

        }

        public void End()
        {
        }
    }
}
