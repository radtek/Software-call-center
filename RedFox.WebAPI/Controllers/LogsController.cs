using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;

using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize]
    public class LogsController : ApiController
    {
        // GET: api/Logs
        public HttpResponseMessage Get(string id)
        {
            var hierarchy = (Hierarchy) LogManager.GetRepository();
            var logger    = (Logger) hierarchy.GetLogger("Audit");
            var appender  = logger.Appenders.OfType<RollingFileAppender>().FirstOrDefault();
            var filename  = appender?.File;
            var json      = "[]";

            if (!string.IsNullOrEmpty(filename))
            {
                json = "[" + File.ReadAllText(filename) + "]";
            }

            return Request.CreateResponse(HttpStatusCode.OK, json, "application/json");
        }

        public HttpResponseMessage Post(string id, [FromBody] string text)
        {
            return Request.CreateResponse(HttpStatusCode.NotImplemented);
        }
    }
}
