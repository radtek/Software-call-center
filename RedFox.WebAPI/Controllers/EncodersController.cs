using log4net;

using RedFox.Core;

using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize]
    public class EncodersController : ApiController
    {
        private static ILog auditLog = LogManager.GetLogger("Audit");

        public HttpResponseMessage Get()
        {
            auditLog.DebugFormat("Request for encoders by {0}", User.Identity.Name);

            IQueryable content = null;

            if (User.IsInRole(Role.Administrator))
            {
                content = new Entities().Encoders.Select(encoder => new
                {
                    encoder.Id,
                    encoder.Name,
                    Station = encoder.Station.Name
                });
            }

            else if (User.IsInRole(Role.Customer))
            {
                content = new Entities().Encoders.Where(encoder =>
                    encoder.Station.Customer.AspNetUsers.Any(user => 
                        user.UserName == User.Identity.Name)).Select(encoder => new
                        {
                            encoder.Id,
                            encoder.Name,
                            Station = encoder.Station.Name
                        });
            }

            else if (User.IsInRole(Role.Station))
            {
                content = new Entities().Encoders.Where(encoder =>
                    encoder.Station.AspNetUsers.Any(user =>
                        user.UserName == User.Identity.Name)).Select(encoder => new
                        {
                            encoder.Id,
                            encoder.Name,
                            Station = encoder.Station.Name
                        });
            }

            var entities = new Entities();
            var encoders = entities.Encoders;
            
            foreach (var encoder in encoders)
            {
                var station = encoder.Station;
                var users   = station.AspNetUsers;

                foreach (var user in users)
                {
                    var name  = user.UserName;
                    var found = name.Equals(User.Identity.Name);
                }
            }
            
            return Request.CreateResponse(HttpStatusCode.OK, content, "application/json");
        }

        public HttpResponseMessage Get(int id)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new Entities().Encoders.Find(id), "application/json");
        }

        public async Task<HttpResponseMessage> Post([FromBody] Encoder encoder)
        {
            var entities = new Entities();
            entities.Encoders.Add(encoder);
            await entities.SaveChangesAsync();

            return Request.CreateResponse(HttpStatusCode.Created);
        }

        public async Task<HttpResponseMessage> Put(int id, [FromBody] Encoder encoder)
        {
            var entities = new Entities();
            var _encode           = entities.Encoders.FirstOrDefault(e=>e.Id == encoder.Id);
            _encode.EncoderTypeId = encoder.EncoderTypeId;
            _encode.IpAddress     = encoder.IpAddress;
            _encode.Name          = encoder.Name;
            _encode.Port          = encoder.Port;
            _encode.PhoneNumber   = encoder.PhoneNumber;
            _encode.StationId     = encoder.StationId;
             


            await entities.SaveChangesAsync();

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        public async Task<HttpResponseMessage> Delete(int id)
        {
            var entities = new Entities();
            var encoder  = new Encoder { Id = id };

            entities.Entry(encoder).State = EntityState.Deleted;
            await entities.SaveChangesAsync();

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }
    }

    [Authorize]
    public class EncoderTypesController : ApiController
    {
        private static ILog auditLog = LogManager.GetLogger("Audit");

        public HttpResponseMessage Get()
        {
            auditLog.DebugFormat("Request for EncoderTypes by {0}", User.Identity.Name);

            var content = new Entities().EncoderTypes.Select(encoderType => new
            {
                encoderType.Id,
                encoderType.Name,
                encoderType.Protocol,
                encoderType.ProviderJson
            });

            return Request.CreateResponse(HttpStatusCode.OK, content, "application/json");
        }
    }
}
