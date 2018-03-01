using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RedFox.Core;
using RedFox.Json;
using System.Collections;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Http;
using RedFox.Json.Serialization;

namespace RedFox.WebAPI.Controllers
{
    [Authorize]
    public class StationsController : ApiController
    {
        private static ILog   systemLog  = LogManager.GetLogger("System");
        private static ILog   auditLog   = LogManager.GetLogger("Audit");
        private static string LOG_PREFIX = "";

        static StationsController()
        {
            var assembly = System.Reflection.Assembly.GetCallingAssembly();
            var name     = assembly.GetName();

            LOG_PREFIX = string.Format("WebAPI [{0} {1}]", name.Name, name.Version);
        }

        // GET: api/Stations
        public HttpResponseMessage Get()
        {
            auditLog.DebugFormat("Station list request by {0}", User.Identity.Name);

            var         entities = new Entities();
            IEnumerable content  = null;

            if (User.IsInRole("Station"))
            {
                var station = entities.Stations.FirstOrDefault(s => s.AspNetUsers.Any(u => u.UserName == User.Identity.Name));

                content = new Entities().Stations.Where(t => t.Id == station.Id).AsEnumerable().Select(t =>
                {
                    return new
                    {
                        t.Id,
                        t.Name,
                        Sessions      = t.Sessions.Count,
                        Consumer      = t.ConsumerJson,
                        Notifications = t.NotificationsJson,
                        Encoders      = t.Encoders.ToList().Count
                    };
                });
            }

            if (User.IsInRole("Customer"))
            {
                var customer = entities.Customers.FirstOrDefault(t => t.AspNetUsers.Any(u => u.UserName == User.Identity.Name));

                content = new Entities().Stations.Where(t => t.CustomerId == customer.Id).AsEnumerable().Select(t =>
                {
                    return new
                    {
                        t.Id,
                        t.Name,
                        Sessions      = t.Sessions.Count,
                        Consumer      = t.ConsumerJson,
                        Notifications = t.NotificationsJson,
                        Encoders      = t.Encoders.ToList().Count
                    };
                });
            }

            if (User.IsInRole("Administrator"))
            {
                content = new Entities().Stations.AsEnumerable().Select(t =>
                {
                    return new
                    {
                        t.Id,
                        t.Name,
                        Sessions      = t.Sessions.Count,
                        Consumer      = t.ConsumerJson,
                        Notifications = t.NotificationsJson,
                        Encoders      = t.Encoders.ToList().Count
                    };
                });
            }

            var formatter = new JsonMediaTypeFormatter()
            {
                SerializerSettings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects
                }
            };

            return Request.CreateResponse(HttpStatusCode.OK, content, formatter, "application/json");
        }

        // GET: api/Stations/5
        public HttpResponseMessage Get(int id)
        {
            auditLog.DebugFormat("Details for Station {0} list requested by {1}", id, User.Identity.Name);

            var entities = new Entities();
            var station  = entities.Stations.Find(id);

            // TODO Authorize Customer OR Station!
            var customer = entities.Customers.FirstOrDefault(t =>
                t.AspNetUsers.Any(u =>
                    u.UserName == User.Identity.Name));

            if (customer == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Station - Customer mismatch");
            }

            if (station == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            return Request.CreateResponse(HttpStatusCode.OK, station, "application/json");
        }

        // POST: api/Stations
        public async Task<HttpResponseMessage> Post([FromBody] JObject value)
        {
            var entities = new Entities();
            var customer = entities.Customers.FirstOrDefault(t => 
                t.AspNetUsers.Any(u => 
                    u.UserName == User.Identity.Name));

            if (customer == null)
            {
                // TODO
            }

            // Parse
            var station = new Station
            {
                CustomerId   = customer.Id,
                ConsumerJson = value["Consumer"].ToString(),
                Name         = value["Name"].ToString()
            };
            
            // Persist
            entities.Stations.Add(station);
            
            var affected = await entities.SaveChangesAsync();

            if (affected == 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Station not created");
            }

            auditLog.InfoFormat("New Station '{0}' ({1}) created by {2}", station.Name, station.Id, User.Identity.Name);
            
            return Request.CreateResponse(HttpStatusCode.Created);
        }

        // PUT: api/Stations/5
        public async Task<HttpResponseMessage> Put(int id, [FromBody] JObject value)
        {
            var entities = new Entities();
            var station  = entities.Stations.SingleOrDefault(s => s.Id == id);
            var customer = entities.Customers.FirstOrDefault(t =>
                t.AspNetUsers.Any(u =>
                    u.UserName == User.Identity.Name));
            
            if (station == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            if (customer == null || customer.Id != station.CustomerId)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Station - Customer mismatch");
            }

            station.ConsumerJson = value["Consumer"].ToString();
            station.Name         = value["Name"].Value<string>();

            var affected = await entities.SaveChangesAsync();

            if (affected == 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Station not updated");
            }

            auditLog.InfoFormat("Station '{0}' ({1}) update by {2}", station.Name, station.Id, User.Identity.Name);

            return Request.CreateResponse(HttpStatusCode.OK);
        }
        
        // DELETE: api/Stations/5
        [HttpDelete]
        public async Task<HttpResponseMessage> Delete(int id)
        {
            var entities = new Entities();
            var station  = entities.Stations.Find(id);
            var customer = entities.Customers.SingleOrDefault(t =>
                t.AspNetUsers.Any(u =>
                    u.UserName == User.Identity.Name));
            
            if (customer == null)
            {
                // TODO Handle 401
            }

            if (station == null)
            {
                // TODO Handle 404
            }

            if (station.CustomerId != customer.Id)
            {
                // TODO Handle 401
            }

            // TODO Delete all scheduled jobs that intended to use this station
            //StdSchedulerFactory.GetDefaultScheduler().DeleteJob(new JobKey(id.ToString(), station.CustomerId.ToString()));

                  entities.Stations.Remove(station);
            await entities.SaveChangesAsync();

            auditLog.InfoFormat("Station '{0}' ({1}) delete by {2}", station.Name, station.Id, User.Identity.Name);

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }
        
        [HttpGet, Route("api/stations/{id}/encoders")]
        public HttpResponseMessage Encoders(int id)
        {
            var encoders = new Entities().Encoders.Where(e => e.StationId == id);
            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                MaxDepth              = 2
            };

            try
            {
                var serializer = new Json.JsonSerializer(settings);
                var result     = serializer.SerializeObject(encoders);
                
                return Request.CreateResponse(HttpStatusCode.OK, result, "application/json");
            }
            catch (Exception e)
            {
                systemLog.Error($"{ LOG_PREFIX } Could not serialize encoders; { e.Message }");
                systemLog.Debug(e.StackTrace);

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
        }
    }
}
