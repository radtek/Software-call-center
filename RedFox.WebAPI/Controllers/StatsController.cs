using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RedFox.Core;

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    public class StatsController : ApiController
    {
        private static ILog auditLog  = LogManager.GetLogger("Audit");
        private static ILog systemLog = LogManager.GetLogger("System");

        public HttpResponseMessage Get()
        {
            var entities = new Entities();
            var sessions = entities.Sessions.Where(s => s.StartTime.Value.Month == DateTime.Now.Month).ToList();
            var minutes  = sessions.Sum(s => s.Elapsed.TotalMinutes);
            var words    = sessions.Sum(s => s.WordCount);
            var cash     = 0D;

            foreach (var session in sessions)
            {
                var rate    = entities.Rates.OrderByDescending(r => r.Valid).FirstOrDefault(r => r.Valid < session.StartTime);
                var rates   = JsonConvert.DeserializeObject<JArray>(rate.Rates).OrderBy(r => r["From"]);
                var counter = session.Elapsed.TotalMinutes;

                foreach (var r in rates)
                {
                    if (counter > r["To"].Value<int>())
                    {
                        cash += r["Cost"].Value<float>() * (r["To"].Value<int>() - r["From"].Value<int>() + 1);
                        counter -= r["To"].Value<int>();
                    }
                    else
                    {
                        cash += r["Cost"].Value<float>() * counter;
                        break;
                    }
                }
            }
            
            // TODO If customer is in a group; then check rules?

            var result = new
            {
                Minutes = Math.Round(minutes, 2),
                Words   = words,
                Cost    = Math.Round(cash, 2)
            };

            return Request.CreateResponse(HttpStatusCode.OK, result, "application/json");
        }
    }
}
