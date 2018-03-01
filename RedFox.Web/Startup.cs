using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(RedFox.Web.Startup))]
namespace RedFox.Web
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
