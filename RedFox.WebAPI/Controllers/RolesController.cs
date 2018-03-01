using log4net;

using Newtonsoft.Json;

using RedFox.Core;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class RolesController : ApiController
    {
        private static ILog auditLog = LogManager.GetLogger("Audit");

        public HttpResponseMessage Get()
        {
            auditLog.DebugFormat("Request for user roles by {1}", User.Identity.Name);
            
            var content = new Entities().AspNetRoles.Select(r => new
            {
                r.Id,
                r.Name
            });

            return Request.CreateResponse(HttpStatusCode.OK, content, "application/json");
        }
    }
}
