using Newtonsoft.Json;

using System.IO;

namespace RedFox.Json
{
    internal class MaxDepthJsonTextWriter : JsonTextWriter
    {
        public MaxDepthJsonTextWriter(TextWriter textWriter) : base(textWriter) { }

        public int CurrentDepth { get; private set; }

        public override void WriteStartObject()
        {
            CurrentDepth++;
            base.WriteStartObject();
        }

        public override void WriteEndObject()
        {
            CurrentDepth--;
            base.WriteEndObject();
        }
    }
}
