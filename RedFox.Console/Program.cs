using System;

namespace RedFox.Console
{
    class Program
    {
        private static Server.Service service;

        static void Main(string[] args)
        {
            System.Console.WriteLine("RedFox Console Wrapper for RedFox Server");
            System.Console.WriteLine("Use Ctrl-C for graceful shutdown");
            System.Console.WriteLine("");

            System.Console.CancelKeyPress += Console_CancelKeyPress;

            service = new Server.Service();
            service.Start();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (service != null)
                service.Stop();

            Environment.Exit(0);
        }
    }
}
