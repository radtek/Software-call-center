using log4net;

using Microsoft.AspNet.Identity.Owin;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RedFox.Core;

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AccountsController : ApiController
    {
        private static ILog auditLog  = LogManager.GetLogger("Audit");
        private static ILog systemLog = LogManager.GetLogger("System");

        public HttpResponseMessage Get()
        {
            auditLog.DebugFormat("Request for user accounts by {0}", User.Identity.Name);

            var content = new Entities().AspNetUsers
                .AsEnumerable()
                .Select(u =>
                {
                    return new
                    {
                        u.Id,
                        u.Email,
                        u.EmailConfirmed,
                        u.LockoutEnabled,
                        u.LockoutEndDateUtc,
                        u.PhoneNumber,
                        u.PhoneNumberConfirmed,
                        u.TwoFactorEnabled,
                        u.UserName
                    };
                });
            
            return Request.CreateResponse(HttpStatusCode.OK, content, "application/json");
        }

        public HttpResponseMessage Get(string id)
        {
            auditLog.DebugFormat("Request for user account {0} by {1}", id, User.Identity.Name);

            var content  = new Entities().AspNetUsers.SingleOrDefault(u => u.Id == id);
            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            return Request.CreateResponse(HttpStatusCode.OK, content, "application/json");
        }

        public async Task<HttpResponseMessage> Post([FromBody] JObject data)
        {
            var userName = data["UserName"].Value<string>();
            var password = data["Password"].Value<string>();
            var customer = data["CustomerId"].Value<int>();
            var station  = data["StationId"].Value<int>();
            var role     = data["Role"]["Name"].Value<string>();

            auditLog.InfoFormat("New user account {0} created by {1}", userName, User.Identity.Name);
            
            var manager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            var user    = new Models.ApplicationUser
            {
                Email            = data["Email"].Value<string>(),
                PhoneNumber      = data["Phone"].Value<string>(),
                TwoFactorEnabled = data["TwoFactorEnabled"].Value<bool>(),
                UserName         = userName,
            };
            
            var result   = await manager.CreateAsync(user, password);
            var entities = new Entities();

            if (!result.Succeeded)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, result.Errors);
            }
            
            if (customer > 0)
            {
                var c = await entities.Customers.FindAsync(customer);
                var u = await entities.AspNetUsers.FindAsync(user.Id);

                if (c != null && u != null)
                    c.AspNetUsers.Add(u);
            }

            if (station > 0)
            {
                var s = await entities.Stations.FindAsync(station);
                var u = await entities.AspNetUsers.FindAsync(user.Id);

                if (s != null && u != null)
                    s.AspNetUsers.Add(u);
            }

            await entities.SaveChangesAsync();

            if (string.IsNullOrEmpty(role))
            {
                return Request.CreateResponse(HttpStatusCode.Created);
            }

            var added = await manager.AddToRoleAsync(user.Id, role);

            if (added.Succeeded)
            {
                return Request.CreateResponse(HttpStatusCode.Created);
            }

            return Request.CreateResponse(HttpStatusCode.BadRequest, result.Errors);
        }
    }
}
