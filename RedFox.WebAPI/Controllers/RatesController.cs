
using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RedFox.Core;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class RatesController : ApiController
    {
        private static ILog auditLog  = LogManager.GetLogger("Audit");
        private static ILog systemLog = LogManager.GetLogger("System");

        public HttpResponseMessage Get()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new Entities().Rates.OrderBy(r => r.Valid), "application/json");
        }

        public HttpResponseMessage Get(int id)
        {
            var rate = new Entities().Rates.Find(id);

            if (rate == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            var rates = (JArray) JsonConvert.DeserializeObject(rate.Rates);
            var data  = new
            {
                Rates = rates,
                Meta  = new { rate.Valid }
            };
            
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(data)) };
        }

        public async Task<HttpResponseMessage> Put([FromBody] JObject data)
        {
            // NB! Date is UTC - auto-converted from local time
            var entities = new Entities();
            var date     = data["Meta"]["Valid"].Value<DateTime>();
            var rate     = entities.Rates.OrderByDescending(r => r.Valid).FirstOrDefault(r => r.Valid == date);

            rate.Rates = JsonConvert.SerializeObject(data["Rates"]);

            await entities.SaveChangesAsync();

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        public async Task<HttpResponseMessage> Post([FromBody] JObject data)
        {
            var entities = new Entities();

            // NB! Date is UTC - auto-converted from local time

            entities.Rates.Add(new Rate()
            {
                Rates = JsonConvert.SerializeObject(data["Rates"]),
                Valid = data["Meta"]["Valid"].Value<DateTime>()
            });

            await entities.SaveChangesAsync();

            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}
