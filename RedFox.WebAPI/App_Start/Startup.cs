using Microsoft.Owin; 
using Microsoft.Owin.Security.OAuth;

using Owin;

using RedFox.WebAPI.Database;
using RedFox.WebAPI.Security;

using System;
using System.Net.Http.Formatting;
using System.Web.Http;

namespace RedFox.WebAPI
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            appBuilder.CreatePerOwinContext(ApplicationDbContext.Create);
            appBuilder.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);

            var config = new HttpConfiguration();

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                         name: "DefaultApi",
                routeTemplate: "api/{controller}"
            );

            config.Routes.MapHttpRoute(
                         name: "EntityApi",
                routeTemplate: "api/{controller}/{id}"
            );
            
            config.Formatters.Add(new BsonMediaTypeFormatter());
            config.Formatters.JsonFormatter
                .SerializerSettings
                .ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            
            appBuilder.UseOAuthAuthorizationServer(new OAuthAuthorizationServerOptions
            {
                        AllowInsecureHttp = true,
                        TokenEndpointPath = new PathString("/oauth/token"),
                AccessTokenExpireTimeSpan = TimeSpan.FromMinutes(30),
                                 Provider = new CustomAuthorizationServerProvider(),
                     RefreshTokenProvider = new CustomRefreshTokenProvider()
            });

            appBuilder.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions());
            appBuilder.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);
            appBuilder.UseWebApi(config);
        }
    }
}
