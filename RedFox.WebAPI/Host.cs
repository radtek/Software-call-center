using Microsoft.Owin.Host.HttpListener;
using Microsoft.Owin.Hosting;

namespace RedFox.WebAPI
{
    public class Host
    {
        public void Start()
        {
            var uberSillyNecessity = typeof(OwinHttpListener);
            if (uberSillyNecessity != null) { }

#if DEBUG
            WebApp.Start<Startup>(new StartOptions { Port = 80 });
#else
            WebApp.Start<Startup>("http://+:8080");
#endif
        }
    }
}