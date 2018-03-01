using RedFox.Core;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize]
    public class ExtensionsController : ApiController
    {
        public HttpResponseMessage Get(string id)
        {
            if (id == "consumers") return Consumers();
            if (id == "providers") return Providers();

            return null;
        }

        private HttpResponseMessage Consumers()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new Entities().Extensions.Where(e => e.Type == ExtensionType.Consumer), "application/json");
        }

        private HttpResponseMessage Providers()
        {
            return Request.CreateResponse(HttpStatusCode.OK, new Entities().Extensions.Where(e => e.Type == ExtensionType.Provider), "application/json");
        }
    }
}
