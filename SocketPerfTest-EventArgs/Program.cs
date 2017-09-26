using System;
using System.Net;
using System.Threading;

namespace SslStreamPerf
{
    class Program
    {
        static void RunServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5000);

            Console.WriteLine($"Running server on {endPoint} (raw)");
            ServerListener.Run(endPoint, null);

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);
        }

        static void Main(string[] args)
        {
            RunServer();
        }
    }
}
