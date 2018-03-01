using System;

namespace RedFox.Core
{
    public partial class Server
    {
        private static Lazy<Server> instance;

        public static Server Instance
        {
                     get { return instance?.Value; }
            internal set { instance = new Lazy<Server>(() => value); }
        }
    }
}
