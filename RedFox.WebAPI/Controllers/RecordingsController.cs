using log4net;
using Newtonsoft.Json.Linq;
using RedFox.Core;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Xml;

namespace RedFox.WebAPI.Controllers
{
    [Authorize]
    public class RecordingsController : ApiController
    {
        private static ILog systemLog  = LogManager.GetLogger("System");
        private static ILog sessionLog = LogManager.GetLogger("Session");
        private static ILog auditLog   = LogManager.GetLogger("Audit");

        // GET: api/Sessions/5
        public HttpResponseMessage Get(int id)
        {
            auditLog.DebugFormat("Recording for Session {0} requested by {1}", id, User.Identity.Name);

            Session session = null;

            if (User.IsInRole(Role.Administrator))
            {
                session = new Entities().Sessions.Find(id);
            }

            else if (User.IsInRole(Role.Customer))
            {
                session = new Entities().Sessions
                    .Include("Station")
                    .Where(t => t.Station.Customer.AspNetUsers.Any(u => u.UserName == User.Identity.Name))
                    .Where(t => t.Id == id)
                    .Where(t => t.Path != "").FirstOrDefault();
            }

            else if (User.IsInRole(Role.Station))
            {
                session = new Entities().Sessions
                    .Include("Station")
                    .Where(t => t.Station.AspNetUsers.Any(u => u.UserName == User.Identity.Name))
                    .Where(t => t.Id == id)
                    .Where(t => t.Path != "").FirstOrDefault();
            }

            if (session == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var url = session.Path;
            //var policy       = CreatePolicyStatement(bucket, id);
            //var bufferPolicy = Encoding.ASCII.GetBytes(policy);
            //var base64       = ToUrlSafeBase64String(bufferPolicy);
            //var signature    = CreateSignature(bufferPolicy)
            //var keypairid    = ;
            //var url          = string.Format("https://s3.amazonaws.com/{0}/{1}.wav?Policy={2}&Signature={3}&Key-Pair-Id={4}", bucket, id, base64, signature, keypairid);
            
            return Request.CreateResponse(HttpStatusCode.OK, url);
        }
        
        public async Task<HttpResponseMessage> Put(int id, [FromBody] JObject content)
        {
            auditLog.DebugFormat("Recording for Session {0} updated by {1}", id, User.Identity.Name);

            var entities = new Entities();
            var session  = entities.Sessions.Find(id);
            
            if (session == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            session.Path = content["Path"].Value<string>();

            entities.Sessions.Attach(session);

            entities.Entry(session).Property(p => p.Path).IsModified      = true;
            entities.Entry(session).Property(p => p.WordCount).IsModified = false;

            await entities.SaveChangesAsync();

            return Request.CreateResponse(HttpStatusCode.Accepted);
        }

        private string CreatePolicyStatement(string bucket, int id)
        {
            var clientip = GetClientIp();
            var resource = string.Format("https://s3.amazonaws.com/{0}/{1}.wav", bucket, id);
            var enddate  = (DateTime.UtcNow.AddMinutes(5)) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            return string.Format("{{\"Statement\":[{{\"Resource\":\"{0}\",\"Condition\":{{\"DateLessThan\":{{\"AWS:EpochTime\":{1}}},\"IpAddress\":{{\"AWS:SourceIp\":\"{2}/32\"}}}}}}]}}", resource, enddate, clientip);
        }

        private string ToUrlSafeBase64String(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('=', '_')
                .Replace('/', '~');
        }

        private string CreateSignature(byte[] bufferPolicy)
        {
            byte[] bufferPolicyHash;

            using (SHA1CryptoServiceProvider cryptoSHA1 = new SHA1CryptoServiceProvider())
            {
                bufferPolicyHash = cryptoSHA1.ComputeHash(bufferPolicy);

                var providerRSA   = new RSACryptoServiceProvider();
                var xmlPrivateKey = new XmlDocument();

                xmlPrivateKey.Load("PrivateKey.xml");

                providerRSA.FromXmlString(xmlPrivateKey.InnerXml);

                var RSAFormatter = new RSAPKCS1SignatureFormatter(providerRSA);

                RSAFormatter.SetHashAlgorithm("SHA1");

                var signedHash      = RSAFormatter.CreateSignature(bufferPolicyHash);
                var strSignedPolicy = ToUrlSafeBase64String(signedHash);

                return strSignedPolicy;
            }
        }

        private string GetClientIp()
        {
            if (Request.Properties.ContainsKey("MS_HttpContext"))
            {
                return ((HttpContextWrapper) Request.Properties["MS_HttpContext"]).Request.UserHostAddress;
            }

            if (Request.Properties.ContainsKey(RemoteEndpointMessageProperty.Name))
            {
                return ((RemoteEndpointMessageProperty) Request.Properties[RemoteEndpointMessageProperty.Name]).Address;
            }

            if (HttpContext.Current != null)
            {
                return HttpContext.Current.Request.UserHostAddress;
            }

            return null;
        }
    }
}
