using System;

namespace RedFox.ControllerHost
{
    class Program
    {
        private static Controller.Service service;

        static void Main(string[] args)
        {
            Console.WriteLine("RedFox Console Wrapper for RedFox Controller");
            Console.WriteLine("Use Ctrl-C for graceful shutdown");
            Console.WriteLine("");

            Console.CancelKeyPress += Console_CancelKeyPress;

            service = new Controller.Service();
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
