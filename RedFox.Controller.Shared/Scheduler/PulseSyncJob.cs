using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;
using RedFox.Core;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RedFox.Controller.Shared.Scheduler
{
    // TODO Should integration with Pulse be a plug-in?
    public class PulseSyncJob : IJob
    {
        private System.Uri                         baseAddress;
        private List<KeyValuePair<string, string>> pairs;
        
        public PulseSyncJob()
        {
            var api_key   = ConfigurationManager.AppSettings["RedFox.Integrations.Pulse.api_key"];
            var api_token = ConfigurationManager.AppSettings["RedFox.Integrations.Pulse.api_token"];

            baseAddress = new System.Uri(ConfigurationManager.AppSettings["RedFox.Integrations.Pulse.baseAddress"]);
            pairs       = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("api_key", api_key),
                new KeyValuePair<string, string>("api_token", api_token)
            };
        }
        
        public void Execute(IJobExecutionContext context)
        {
            //Task.Run(() => GetClients());
        }
                
        private async void GetClients()
        {
            var response = await Post("client");

            if (!response.IsSuccessStatusCode)
            {
                // TODO Error handling
                return;
            }
                
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(await response.Content.ReadAsStringAsync());

            using (var entities = new Entities())
            {
                var tasks = new Task[dict.Keys.Count];

                for (var i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = GetClient(dict.Keys.ElementAt(i), entities);
                }

                Task.WaitAll(tasks);

                await entities.SaveChangesAsync();
            }
        }

        private async Task GetClient(string id, Entities entities)
        {
            var response = await Post("client/" + id);

            if (!response.IsSuccessStatusCode)
            {
                // TODO Error handling
                return;
            }

            var json     = await response.Content.ReadAsStringAsync();
            var jObject  = JObject.FromObject(JsonConvert.DeserializeObject(json));
            var clientId = jObject["ClientID"].Value<int>();
            var station  = entities.Stations.SingleOrDefault(s => s.PulseId == clientId);

            if (station == null)
            {
                station = new Station
                {
                    Name     = jObject["ClientName"].Value<string>(),
                    PulseId  = jObject["ClientId"].Value<int>(),
                    Location = jObject["StationLocation"].Value<string>()
                    
                };
            }

            // TODO How do we know customer ID? 
            
            station.ConsumerJson = JsonConvert.SerializeObject(new
            {
                Name      = "Dial-up",
                Direction = "Out",
                Endpoint  = "+1" + jObject["Audio"].Value<string>().Replace("-", string.Empty)
            });

            if (station.Encoders.Count == 0)
            {
                station.Encoders.Add(new Encoder()
                {
                    Name          = jObject["EncoderType"].Value<string>(),
                    EncoderTypeId = entities.EncoderTypes.SingleOrDefault(t => t.Protocol == ProtocolType.Phone).Id
                });
            }

            var encoder = station.Encoders.FirstOrDefault();

            encoder.PhoneNumber   = jObject["Encoder"].Value<string>();
            //encoder.Settings      = jObject["Settings"].Value<string>();
            //encoder.LinePlacement = jObject["LinePlacement"].Value<string>();

            entities.Stations.Add(station);
        }

        private async Task<HttpResponseMessage> Post(string url)
        {
            using (var client  = new HttpClient { BaseAddress = baseAddress }) 
                using (var content = new FormUrlEncodedContent(pairs))
                    return await client.PostAsync(url, content);
        }
    }
}
