﻿using System;
using System.Net;
using System.Threading;

namespace SocketPerfTest
{
    class Program
    {
        static void RunServer()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5000);

            Console.WriteLine($"Running server on {endPoint} (raw)");
            ServerListener.Run(endPoint);

            Console.WriteLine($"Server running");

            Thread.Sleep(Timeout.Infinite);
        }

        static void Main(string[] args)
        {
            RunServer();
        }
    }
}
