using Newtonsoft.Json.Linq;

using RedFox.Consumers;

using System;
using System.Data.Entity;
using System.Linq;

namespace RedFox.Core
{
    public partial class Station
    {
        //private Lazy<IConsumer, IConsumerMetadata> consumer;

        public static Station FromEndpoint(string endpoint)
        {
            Station station = null;

            using (var entities = new Entities())
            {
                // Find a station where the ConsumerJson contains an endpoint that
                // matches the method parameter

                // TODO Must find a more secure way to find the endpoint in ConsumerJson
                station = entities.Stations
                    .Include(e => e.Encoders.Select(t => t.EncoderType))
                    .FirstOrDefault(s => s.ConsumerJson.Contains(endpoint));
            }

            return station;
        }

        //public Lazy<IConsumer, IConsumerMetadata> Consumer
        //{
        //    get
        //    {
        //        if (consumer == null)
        //        {
        //            var name = JObject.Parse(ConsumerJson)
        //                .SelectToken("Name")
        //                .Value<string>();




        //            consumer = Shared.Extensions.Instance.Consumers
        //                .Where(t => t.Metadata.Name == name)
        //                .FirstOrDefault();
        //        }  

        //        return consumer;
        //    }
        //}
    }
}
