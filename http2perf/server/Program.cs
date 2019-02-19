using System;
using System.Net;

namespace customserver
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Server starting up...");

            var server = new HttpServer(new IPEndPoint(IPAddress.Loopback, 5001));
            server.Run();

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }
    }
}
