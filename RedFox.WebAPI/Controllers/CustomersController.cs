using log4net;

using RedFox.Core;

using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace RedFox.WebAPI.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class CustomersController : ApiController
    {
        private static ILog auditLog = LogManager.GetLogger("Audit");

        public HttpResponseMessage Get()
        {
            auditLog.DebugFormat("Request for customers by {0}", User.Identity.Name);

            var content = new Entities().Customers.Select(c => new
            {
                c.Id,
                c.Name,
                StationCount = c.Stations.Count,
            });

            return Request.CreateResponse(HttpStatusCode.OK, content, "application/json");
        }

        public HttpResponseMessage Get(int id)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new Entities().Customers.Find(id), "application/json");
        }

        public async Task<HttpResponseMessage> Post([FromBody] Customer customer)
        {
            var entities = new Entities();

            entities.Customers.Add(customer);
            await entities.SaveChangesAsync();

            return Request.CreateResponse(HttpStatusCode.Created);
        }

        public async Task<HttpResponseMessage> Put(int id, [FromBody] Customer customer)
        {
            var entities = new Entities();
            try
            {
                customer.AspNetUsers = null;
                customer.Stations    = null;

                entities.Customers.Attach(customer);
                entities.Entry(customer).State = EntityState.Modified;

                await entities.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        public async Task<HttpResponseMessage> Delete(int id)
        {
            try
            {
                var entities = new Entities();
                var customer = new Customer { Id = id };

                entities.Entry(customer).State = EntityState.Deleted;
                await entities.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }
    }
}
