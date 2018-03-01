using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RedFox.Core;

namespace RedFox.Controller.Messages
{
    public class Composer
    {
        /// <summary>
        /// Response from Controller to Server after successful server registration
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public string RegisterServerResponse(Server server)
        {
            dynamic json = new JObject();

            json.Type   = "RegisterServerResponse";
            json.Server = JsonConvert.SerializeObject(new { Id = server.Id });

            return JsonConvert.SerializeObject(json);
        }

        /// <summary>
        /// Request from Controller to Server to start a new session
        /// </summary>
        /// <param name="workflow"></param>
        /// <returns></returns>
        public string SessionStartRequest(Session session)
        {
            dynamic json = new JObject();
            dynamic ssn  = JObject.FromObject(session, new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            dynamic stn  = JObject.FromObject(session.Station, new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            dynamic enc  = JObject.FromObject(session.Encoder, new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            
            json.Type    = "SessionStartRequest";
            json.Session = ssn;

            json.Session.Station = stn;
            json.Session.Encoder = enc; 
            
            return JsonConvert.SerializeObject(json, Formatting.None, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        }
    }
}
