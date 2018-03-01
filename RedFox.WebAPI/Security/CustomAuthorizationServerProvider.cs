using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OAuth;
using RedFox.Core;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RedFox.WebAPI.Security
{
    public class CustomAuthorizationServerProvider : OAuthAuthorizationServerProvider
    {
        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
        {
            context.Validated();

            return Task.FromResult(0);
        }

        public override Task MatchEndpoint(OAuthMatchEndpointContext context)
        {
            // TODO 
            context.OwinContext.Response.Headers.Add(new KeyValuePair<string, string[]>("Access-Control-Allow-Origin", new[] { "*" }));

            return base.MatchEndpoint(context);
        }

        public override Task TokenEndpoint(OAuthTokenEndpointContext context)
        {
            foreach (var pair in context.Properties.Dictionary)
            {
                context.AdditionalResponseParameters.Add(pair.Key, pair.Value);
            }
            
            return base.TokenEndpoint(context);
        }

        public override Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
        {
            //context.OwinContext.Request.RemoteIpAddress is the IP of the web server!

            var userManager = context.OwinContext.GetUserManager<ApplicationUserManager>();
            var user        = userManager.FindAsync(context.UserName, context.Password);
            var roles       = "";

            if (user.Result == null)
            {
                context.SetError("invalid_grant", "Username and password do not match.");
                return Task.FromResult(0);
            }
            
            foreach (var role in userManager.GetRolesAsync(user.Result.Id).Result)
            {
                roles += role + ",";
            }

            var organization = "Unkown";
            var entities     = new Entities();
            var dbuser       = entities.AspNetUsers.FirstOrDefault(u => u.UserName == context.UserName);
            
            if (dbuser != null && roles.Contains("Station"))
            {
                organization = dbuser.Stations.FirstOrDefault()?.Name;
            }
            
            if (roles.Contains("Customer"))
            {
                organization = dbuser.Customers.FirstOrDefault()?.Name;
            }

            if (roles.Contains("Administrator"))
            {
                organization = "Administrator";
            }

            var identity   = userManager.CreateIdentityAsync(user.Result, OAuthDefaults.AuthenticationType);
            var properties = new AuthenticationProperties(new Dictionary<string, string>
            {
                {           "id", user.Result.Id       },
                {     "username", user.Result.UserName },
                {        "roles", roles.TrimEnd(',')   },
                { "organization", organization         }
            });
            
            context.Validated(new AuthenticationTicket(identity.Result, properties));

            return Task.FromResult(0);
        }
    }
}
