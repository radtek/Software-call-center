using Newtonsoft.Json;

using RedFox.Json.Serialization;

using System.IO;

namespace RedFox.Json
{
    public class JsonSerializer : Newtonsoft.Json.JsonSerializer
    {
        public JsonSerializerSettings Settings { get; private set; }

        public JsonSerializer(JsonSerializerSettings settings)
        {
            Settings = settings;
        }

        public string SerializeObject(object obj)
        {
            using (var writer = new StringWriter())
            {
                using (var jsonWriter = new MaxDepthJsonTextWriter(writer))
                {
                    bool     include() => jsonWriter.CurrentDepth <= Settings.MaxDepth;
                    
                    ContractResolver      = new CustomContractResolver(include);
                    ReferenceLoopHandling = Settings.ReferenceLoopHandling;

                    Serialize(jsonWriter, obj);
                }

                return writer.ToString();
            }
        }
    }
}
