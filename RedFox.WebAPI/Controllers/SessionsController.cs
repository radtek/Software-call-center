using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;

using RedFox.Controller.Scheduler;
using RedFox.Core;

using System;
using System.Linq;
using System.Linq.Dynamic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize]
    public class SessionsController : ApiController
    {
        private static ILog systemLog  = LogManager.GetLogger("System");
        private static ILog sessionLog = LogManager.GetLogger("Session");
        private static ILog auditLog   = LogManager.GetLogger("Audit");

        // GET: api/Sessions
        public HttpResponseMessage Get()
        {
            var parameters = Request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value);

            auditLog.Debug("Session list request by " + User.Identity.Name);

            IQueryable<Session> queryable = null;

            if (User.IsInRole("Station"))
            {
                queryable = new Entities().Sessions.Include("Station")
                    .Where(s => s.Station.AspNetUsers.Any(u => u.UserName == User.Identity.Name));
            }

            else if (User.IsInRole("Customer"))
            {
                queryable = new Entities().Sessions.Include("Station")
                    .Where(s => s.Station.Customer.AspNetUsers.Any(u => u.UserName == User.Identity.Name));
            }

            else if (User.IsInRole("Administrator"))
            {
                queryable = new Entities().Sessions.Include("Station");
            }

            if (parameters.ContainsKey("status"))
            {
                var status = parameters["status"].ToLower();

                queryable = queryable.Where(s => s.State
                    .ToString().ToLower().Equals(status));
            }

            if (parameters.ContainsKey("sort") && !string.IsNullOrEmpty(parameters["sort"]))
            {
                if (parameters["sort"] == "Name")
                    parameters["sort"] = "Station.Name";

                var order = $"{ parameters["sort"] } { parameters["order"] }ending";

                queryable = queryable.OrderBy(order);
            }

            if (parameters.ContainsKey("offset"))
            {
                int.TryParse(parameters["offset"], out int offset);

                if (offset > 0)
                    queryable = queryable.Skip(offset);
            }

            if (parameters.ContainsKey("limit"))
            {
                int.TryParse(parameters["limit"], out int limit);

                if (limit > 0)
                    queryable = queryable.Take(limit);
            }
            
            var content = queryable.AsEnumerable().Select(t =>
            {
                return new
                {
                    t.Station.Name,
                    t.Id,
                    t.Schedule,
                    t.StartTime,
                    t.EndTime,
                    t.Length,
                    Encoder = t.Encoder.Name,
                    Elapsed = t.Elapsed.TotalSeconds,
                    State   = t.State.ToString()
                };
            });
            
            var formatter = new JsonMediaTypeFormatter()
            {
                SerializerSettings = new JsonSerializerSettings
                {
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects
                }
            };

            try
            {
                return Request.CreateResponse(HttpStatusCode.OK, content, formatter, "application/json");
            }
            catch (Exception e)
            {
                systemLog.Error("Error on GET /sessions", e);
                systemLog.Debug(e.StackTrace);

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
        }

        // GET: api/Sessions/5
        public HttpResponseMessage Get(int id)
        {
            auditLog.DebugFormat("Details for Session {0} request by {1}", id, User.Identity.Name);

            var session = new Entities().Sessions
                .Include("Station")
                .Where(t => t.Station.Customer.AspNetUsers.Any(u => u.UserName == User.Identity.Name) || t.Station.AspNetUsers.Any(u => u.UserName == User.Identity.Name))
                .Where(t => t.Id == id)
                .FirstOrDefault();

            if (session == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var formatter = new JsonMediaTypeFormatter()
            {
                SerializerSettings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                }
            };

            var station = new
            {
                session.Station.Name,
                session.Station.Id
            };

            var content = new
            {
                session.Id,
                session.Elapsed,
                session.EndTime,
                session.Schedule,
                session.StartTime,
                session.WordCount,
                session.Record,
                session.Muted,
                session.Paused,

                Station = station,
                State   = session.State.ToString(),
            };
            
            return Request.CreateResponse(HttpStatusCode.OK, content, formatter, "application/json");
        }

        public async Task<HttpResponseMessage> Post([FromBody] JObject content)
        {
            var session  = new Session();
            var entities = new Entities();

            // TODO Verify if User.Identity has access to Station
            try
            {
                if (!content["Accept"].Value<bool>()) return Request.CreateResponse(HttpStatusCode.NotAcceptable);

                var schedule = content["Schedule"];
                var start    = schedule["Start"].Value<DateTime>();
                var length   = schedule["Length"].Value<int>();
                var CRON     = schedule["CRON"].Value<string>();
                var utc      = DateTime.Now.ToUniversalTime();
                var rate     = entities.Rates.OrderByDescending(r => r.Valid).FirstOrDefault(r => r.Valid <= utc);
                
                session.RateId            = rate.Id;
                session.StationId         = content["Station"]["Id"].Value<int>();
                session.EncoderId         = content["Encoder"]["Id"].Value<int>();
                session.State             = SessionState.Scheduled;
                session.Schedule          = start;
                session.Length            = length;
                session.CRON              = CRON;
                session.Muted             = false;
                session.Paused            = false;
                session.ConsumerJson      = content["Consumer"].ToString();
                session.NotificationsJson = content["Notifications"].ToString();
                // We want only the customized properties of the Encoder here; the rest can
                // be fetched via the session.EncoderId
                session.EncoderJson       = JObject.FromObject(new
                {
                    EndPoint  = content["Encoder"]["PhoneNumber"]?.ToString(),
                    IpAddress = content["Encoder"]["IpAddress"]?.ToString(),
                    Port      = content["Encoder"]["Port"]?.Value<int>()
                }).ToString();

                // TODO Check if the encoder isn't in use at scheduled time

                entities.Sessions.Add(session);

                await entities.SaveChangesAsync();

                auditLog.InfoFormat("Session {0} created by {1}", session.Id, User.Identity.Name);
            }
            catch (Exception ex)
            {
                systemLog.Error("Could not create session", ex);

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Could not create this session");
            }

            try
            {
                var scheduler = StdSchedulerFactory.GetDefaultScheduler();
                // TODO Trigger offset must be UTC!
                var offset    = new DateTimeOffset(session.Schedule.Value);
                var trigger   = TriggerBuilder.Create()
                    .WithIdentity(session.Id.ToString(), session.EncoderId.ToString())
                    .WithCronSchedule(session.CRON)
                    .StartAt(offset)
                    .Build();

                var job = JobBuilder.Create<TranscribeJob>()
                    .WithIdentity(session.Id.ToString(), session.EncoderId.ToString())
                    .Build();

                scheduler.ScheduleJob(job, trigger);
                
                return Request.CreateResponse(HttpStatusCode.Created, new { NextFireTimeUtc = trigger.GetNextFireTimeUtc() }, "application/json");
            }
            catch (Exception ex)
            {
                systemLog.Error("Could not schedule session " + session.Id, ex);

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Could not schedule this session");
            }
        }

        // PUT: api/Sessions/5
        public async Task<HttpResponseMessage> Put(int id, [FromBody] JObject content)
        {
            var entities        = new Entities();
            var isAdministrator = User.IsInRole(Role.Administrator);
            var session         = entities.Sessions
                .Where(t => t.Station.Customer.AspNetUsers.Any(u => u.UserName == User.Identity.Name) || t.Station.AspNetUsers.Any(u => u.UserName == User.Identity.Name) || isAdministrator)
                .Where(t => t.Id == id)
                .FirstOrDefault();
            
            if (session == null)
            {
                sessionLog.ErrorFormat("There is no session with ID {0} for User {1}", id, User.Identity.Name);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            auditLog.InfoFormat("Session {0} updated by {1}", id, User.Identity.Name);

            if (session.State == SessionState.Active)
            {
                // 
                var messageClient = Shared.Extensions.Instance.MessageClient;

                if (messageClient == null)
                {
                    sessionLog.Error("The messageclient is unavailable");

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "The message client is unavailable");
                }
                
                var muted  = content["Muted"].Value<bool>();
                var paused = content["Paused"].Value<bool>();
                var record = content["Record"].Value<bool>();
                
                //settings.ProfanityFilter = content["Settings"]["ProfanityFilter"].Value<bool>();
                //settings.SmartFormatting = content["Settings"]["SmartFormatting"].Value<bool>();
                //settings.SpeakerLabels   = content["Settings"]["SpeakerLabels"].Value<bool>();

                try
                {
                    var s = new JObject
                    {
                        { "Id"    , session.Id },
                        { "Muted" , muted  },
                        { "Paused", paused },
                        { "Record", record }
                    };

                    var j = new JObject
                    {
                        { "Type"   , "SessionChangeRequest" },
                        { "Session", s }
                    };

                    messageClient.Value.Send(session.Server.IPv4, JsonConvert.SerializeObject(j));
                }
                catch (Exception ex)
                {
                     systemLog.Error("Error sending message to server at " + session.Server.IPv4, ex);
                    sessionLog.Error("Error sending message to server at " + session.Server.IPv4, ex);

                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                return Request.CreateResponse(HttpStatusCode.Accepted);
            }

            if (session.State == SessionState.Scheduled)
            {
                //
                var schedule       = content["Schedule"];
                var length         = schedule["Length"].Value<int>();

                var end            = schedule["EndTime"].Value<DateTime>();
                var stations       = content["Station"];
                //session.StationId = stations["Id"].Value<int>();
                
                session.State      = SessionState.Scheduled;
                session.CRON       = schedule["CRON"].Value<string>();
                session.Schedule   = schedule["Start"].Value<DateTime>();
                session.Record     = content["Record"].Value<bool>();
                session.Muted      = false;
                session.Paused     = false;
                
                // TODO Fill out all these
                session.Rate       = null;
                session.Encoder    = null;
                session.Server     = null;
                session.Station    = null;
                
                      entities.Sessions.Attach(session);
                      entities.Entry(session).State = System.Data.Entity.EntityState.Modified;
                await entities.SaveChangesAsync();

                // Find the trigger and change
                var scheduler   = StdSchedulerFactory.GetDefaultScheduler();
                var triggerKeys = scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.GroupEquals(session.StationId.ToString()));
                var triggerKey  = triggerKeys.SingleOrDefault(t => t.Name == session.EncoderId.ToString());

                if (triggerKey == null)
                {
                    systemLog.ErrorFormat("Could not find trigger key for session {0}", id);

                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Database updated; but could not find schedule trigger key");
                }

                var offset  = new DateTimeOffset(session.Schedule.Value);
                var trigger = TriggerBuilder.Create()
                    .WithIdentity(session.EncoderId.ToString(), session.StationId.ToString())
                    .StartAt(offset)
                    .Build();

                // TODO Trigger offset must be UTC!
                scheduler.RescheduleJob(triggerKey, trigger);

                return Request.CreateResponse(HttpStatusCode.OK);
            }

            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Cannot make changes to a session that has finished");
        }

        public HttpResponseMessage Delete(int id)
        {
            var entities        = new Entities();
            var isAdministrator = User.IsInRole(Role.Administrator);
            var session         = entities.Sessions
                .Where(t => t.Station.Customer.AspNetUsers.Any(u => u.UserName == User.Identity.Name) || t.Station.AspNetUsers.Any(u => u.UserName == User.Identity.Name) || isAdministrator)
                .Where(t => t.Id == id)
                .FirstOrDefault();
            
            if (session == null)
            {
                sessionLog.ErrorFormat("There is no session with ID {0} for User {1}", id, User.Identity.Name);
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (session.State != SessionState.Active)
            {
                sessionLog.ErrorFormat("Cannot stop session {0} while state is {1}", id, session.State);
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            auditLog.InfoFormat("Session {0} forcefully stopped by {1}", id, User.Identity.Name);
            
            var messageClient = Shared.Extensions.Instance.MessageClient;

            if (messageClient == null)
            {
                sessionLog.Error("The messageclient is unavailable");

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "The message client is unavailable");
            }

            try
            {
                var s = new JObject
                {
                    { "Id" , session.Id }
                };

                var j = new JObject
                {
                    { "Type"   , "SessionEndRequest" },
                    { "Session", s }
                };

                messageClient.Value.Send(session.Server.IPv4, JsonConvert.SerializeObject(j));
            }
            catch (Exception ex)
            {
                 systemLog.Error("Error sending SessionEndRequest to Server " + session.Server.IPv4, ex);
                sessionLog.Error("Error sending SessionEndRequest to Server " + session.Server.IPv4, ex);

                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }
    }
}
